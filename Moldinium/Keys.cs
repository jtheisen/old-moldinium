using System;
using System.Diagnostics;

namespace IronStone.Moldinium
{
    [DebuggerDisplay("{Id}")]
    public struct Key : IEquatable<Key>, IComparable<Key>
    {
        internal Guid Id;

        public static Boolean operator ==(Key lhs, Key rhs)
        {
            return lhs.Id == rhs.Id;
        }

        public static Boolean operator !=(Key lhs, Key rhs)
        {
            return lhs.Id != rhs.Id;
        }

        public override Boolean Equals(Object obj)
        {
            return Id.Equals(obj);
        }

        public override Int32 GetHashCode()
        {
            return Id.GetHashCode();
        }

        public Boolean Equals(Key other)
        {
            return Id.Equals(other.Id);
        }

        public Int32 CompareTo(Key other)
        {
            return Id.CompareTo(other.Id);
        }
    }

    public static class KeyHelper
    {
        public static Key Create()
        {
            return new Key() { Id = Guid.NewGuid() };
        }
    }
}
