namespace SampleApp
{
    using IronStone.Moldinium;
    using System;

    public static class Global
    {
        public static ModelType Create<ModelType>()
                where ModelType : class, IModel
            => Models.Create<ModelType>();

        public static ModelType Create<ModelType>(Action<ModelType> customize)
                where ModelType : class, IModel
            => Models.Create<ModelType>(customize);
    }
}
