using System.Collections.Generic;
using System.Linq;

namespace Utilities {
    public static class StackExtensions {
        public static bool EqualsStack<T>(this Stack<T> stack1, Stack<T> stack2)
        {
            if (stack1 == null || stack2 == null)
            {
                return stack1 == stack2;
            }

            return stack1.SequenceEqual(stack2);
        }
    }
}