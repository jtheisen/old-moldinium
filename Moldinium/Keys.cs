using System;
using System.Diagnostics;

namespace IronStone.Moldinium
{
    [DebuggerDisplay("{id}")]
    public struct Id : IEquatable<Id>, IComparable<Id>
    {
        internal Guid id;

        public static Boolean operator ==(Id lhs, Id rhs)
        {
            return lhs.id == rhs.id;
        }

        public static Boolean operator !=(Id lhs, Id rhs)
        {
            return lhs.id != rhs.id;
        }

        public override Boolean Equals(Object obj)
        {
            return id.Equals(obj);
        }

        public override Int32 GetHashCode()
        {
            return id.GetHashCode();
        }

        public Boolean Equals(Id other)
        {
            return id.Equals(other.id);
        }

        public Int32 CompareTo(Id other)
        {
            return id.CompareTo(other.id);
        }

        public override string ToString()
        {
            return id.ToString().Substring(0, 6);
        }
    }

    public static class IdHelper
    {
        public static Id Create()
        {
            return new Id() { id = Guid.NewGuid() };
        }
    }
}
