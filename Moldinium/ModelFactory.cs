using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Reflection;

namespace IronStone.Moldinium
{
    class ModelFactoryInterceptor : IInterceptor
    {
        public ModelFactoryInterceptor(ModelFactoryProxyGenerator generator)
        {
            this.generator = generator;
        }

        public void Intercept(IInvocation invocation)
        {
            var method = invocation.Method;

            var parts = method.Name.Split('_');

            if (parts.Length != 2) throw new Exception($"Unexpected method encountered: {method.Name}");

            var implementations = GetImplementations(invocation.Proxy, method.DeclaringType);

            switch (parts[0])
            {
                case "get":
                    implementations[method].Get(invocation);
                    break;
                case "set":
                    implementations[method].Set(invocation);
                    break;
                case "add":
                    foreach (var implementation in implementations.Values)
                        implementation.PropertyChanged += (PropertyChangedEventHandler)invocation.Arguments[0];
                    break;
                case "remove":
                    foreach (var implementation in implementations.Values)
                        implementation.PropertyChanged -= (PropertyChangedEventHandler)invocation.Arguments[0];
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        abstract class PropertyImplementation
        {
            public PropertyImplementation(PropertyInfo property, Object target)
            {
                this.target = target;
                eventArgs = new PropertyChangedEventArgs(property.Name);
            }

            public abstract void Get(IInvocation invocation);
            public abstract void Set(IInvocation invocation);

            public event PropertyChangedEventHandler PropertyChanged;

            protected void Notify(Unit unit)
            {
                PropertyChanged?.Invoke(target, eventArgs);
            }

            protected readonly Object target;

            private readonly PropertyChangedEventArgs eventArgs;
        }

        class WatchableVariablePropertyImplementation : PropertyImplementation
        {
            public WatchableVariablePropertyImplementation(PropertyInfo property, Object target)
                : base(property, target)
            {
                variable = WatchableVariable.Create(property.PropertyType);

                variable.Changed.Subscribe(Notify);
            }

            public override void Get(IInvocation invocation)
            {
                invocation.ReturnValue = variable.UntypedValue;
            }

            public override void Set(IInvocation invocation)
            {
                variable.UntypedValue = invocation.Arguments[0];
            }

            WatchableVariable variable;
        }

        class WatchableImplementationPropertyImplementation : PropertyImplementation
        {
            public WatchableImplementationPropertyImplementation(PropertyInfo property, Object target)
                : base(property, target)
            {
                changed.Subscribe(OnChanged);
            }

            public override void Get(IInvocation invocation)
            {
                if (dirty)
                {
                    subscriptions.Disposable = null;

                    var dependencies = Repository.Instance.Evaluate(invocation.Proceed);

                    subscriptions.Disposable = new CompositeDisposable(
                        from w in dependencies select w.Changed.Subscribe(changed));

                    cache = invocation.ReturnValue;

                    dirty = false;
                }
                else
                {
                    invocation.ReturnValue = cache;
                }
            }

            public override void Set(IInvocation invocation)
            {
                invocation.Proceed();
            }

            void OnChanged(Unit unit)
            {
                if (dirty) return;

                dirty = true;

                Notify(unit);
            }

            Boolean dirty = true;

            Object cache;

            Subject<Unit> changed = new Subject<Unit>();

            SerialDisposable subscriptions = new SerialDisposable();
        }


        static Dictionary<MethodInfo, PropertyImplementation> GetImplementations(Object target, Type type)
        {
            ObjectInfo info = GetInfo(target);

            if (info.PropertyImplementations == null)
            {
                info.PropertyImplementations = new Dictionary<MethodInfo, PropertyImplementation>();

                foreach (var property in type.GetProperties())
                {
                    var implementation = MakeImplementation(property, target);

                    foreach (var prefix in new[] { "get", "set" })
                    {
                        var method = type.GetMethod($"{prefix}_{property.Name}");

                        info.PropertyImplementations[method] = implementation;
                    }
                }
            }

            return info.PropertyImplementations;
        }

        static PropertyImplementation MakeImplementation(PropertyInfo property, Object target)
        {
            if (property.GetMethod.IsAbstract)
            {
                return new WatchableVariablePropertyImplementation(property, target);
            }
            else
            {
                return new WatchableImplementationPropertyImplementation(property, target);
            }
        }

        static ObjectInfo GetInfo(Object target)
        {
            ObjectInfo info;

            if (!objectInfos.TryGetValue(target, out info))
            {
                info = objectInfos[target] = new ObjectInfo();
            }

            return info;
        }

        class ObjectInfo
        {
            public Dictionary<MethodInfo, PropertyImplementation> PropertyImplementations;
        }

        static Dictionary<Object, ObjectInfo> objectInfos = new Dictionary<Object, ObjectInfo>();

        ModelFactoryProxyGenerator generator;
    }

    class ModelFactoryProxyGenerator : ProxyGenerator
    {
        public Type GetProxyType(Type modelType)
        {
            return CreateClassProxyType(modelType, new[] { typeof(INotifyPropertyChanged) }, ProxyGenerationOptions.Default);
        }
    }

    public class ModelFactory
    {
        public ModelFactory()
        {
            interceptor = new ModelFactoryInterceptor(generator);
        }

        public ModelType Create<ModelType>()
            where ModelType : class
        {
            var model = (ModelType)generator.CreateClassProxy(typeof(ModelType), new[] { typeof(INotifyPropertyChanged) }, interceptor);

            return model;
        }

        public ModelType Create<ModelType>(Action<ModelType> customize)
            where ModelType : class
        {
            var model = Create<ModelType>();

            customize?.Invoke(model);

            return model;
        }


        public Type GetActualType(Type modelType)
        {
            return generator.GetProxyType(modelType);
        }

        ModelFactoryInterceptor interceptor;

        ModelFactoryProxyGenerator generator = new ModelFactoryProxyGenerator();
    }
}
