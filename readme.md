*WARNING:* It's unlikely that I will develop and support this library any further.

Moldinium
=========

MobX/Knockout-style dependency tracking for .NET.

Also helps with implementing `INotifyPropertyChanged`.

Appveyor build status: ![build-status](https://ci.appveyor.com/api/projects/status/a4c7svxm4q7solua?svg=true)

Show me some code!
==================

Moldinium lets you define classes like this:

```
public abstract class Course : IModel
{
    public abstract String Name { get; set; }

    public abstract Room Location { get; set; }

    public virtual String Description
        => $"course {Name} in room {Location.Name}";
}

public abstract class Room : IModel
{
    public abstract String Name { get; set; }
}
```

And you get the following without any additional code:

- An implementation of `INotifyPropertyChanged` for all properties declared abstract or virtual.
- The property `Course.Description` will automatically update on a change to either `Course.Location` or `Course.Location.Name`.

The first point is realized by runtime code-generation that creates derived types from
your classes - which are thus called archetypes in this context. The derived type then
implements `INotifyPropertyChanged`.

The second point is called dependency tracking.

Do I really need dependency tracking?
=====================================

If you write UIs, you want dependency tracking.

It's awesome, helps even in simple cases, and is almost indispensable for clean code in complex ones.

If you ever wrote some more complex UI where you find yourself sprinkling `UpdateThisAndThatAlso()`
calls in random places as you've lost sight of what excatly needs to be updated when
what other variable changes, you will have thought that there has to be a better way.

Javascript had a better way for a long time as part of the famous [Knockout framework](http://knockoutjs.com/),
and more recently the popular [MobX](https://github.com/mobxjs/mobx) library,
but the technique can be used in any language. If you're interested in the details,
[this article on the Knockout site](http://knockoutjs.com/documentation/computed-dependency-tracking.html)
explains the idea well, although mind that there are also some design differences (eg. regarding laziness).

How do I get started?
=====================

By reading the [Getting Started guide](https://github.com/jtheisen/moldinium/wiki/Getting-started)!
