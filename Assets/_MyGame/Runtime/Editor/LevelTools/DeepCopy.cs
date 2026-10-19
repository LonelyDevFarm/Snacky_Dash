using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Bản sao sâu một đồ thị đối tượng bằng reflection (chỉ dùng trong công cụ Editor): SmartPlayer chép nguyên ván đang chơi để
    /// thử một nước rồi bỏ, khỏi chơi lại từ đầu. Giữ chung: chuỗi, kiểu nguyên thuỷ, asset Unity (UnityEngine.Object — màn, bản đồ);
    /// sự kiện (delegate) bị bỏ trống ở bản sao để view / người nghe của ván thật không chạy theo.
    /// </summary>
    public static class DeepCopy
    {
        private static readonly Dictionary<Type, FieldInfo[]> Fields = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, bool> Plain = new Dictionary<Type, bool>();

        public static T Of<T>(T source) where T : class => (T)Copy(source, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        private static object Copy(object source, Dictionary<object, object> done)
        {
            if (source == null) return null;
            var type = source.GetType();
            if (IsPlain(type) || source is UnityEngine.Object || source is Type) return source;
            if (source is Delegate) return null;
            if (!type.IsValueType && done.TryGetValue(source, out var existing)) return existing;
            if (type.IsArray)
            {
                var array = (Array)source;
                var element = type.GetElementType();
                // Clone giữ đúng số chiều (int[,] khoảng cách khay của GameSession.Shutters); phần tử tham chiếu thì chép từng ô.
                var copy = (Array)array.Clone();
                done[source] = copy;
                if (IsPlain(element)) return copy;
                if (array.Rank == 1) for (int i = 0; i < array.Length; i++) copy.SetValue(Copy(array.GetValue(i), done), i);
                else
                {
                    var index = new int[array.Rank];
                    for (int flat = 0; flat < array.Length; flat++)
                    {
                        for (int r = array.Rank - 1, rest = flat; r >= 0; r--)
                        {
                            int length = array.GetLength(r);
                            index[r] = array.GetLowerBound(r) + rest % length;
                            rest /= length;
                        }
                        copy.SetValue(Copy(array.GetValue(index), done), index);
                    }
                }
                return copy;
            }
            var clone = FormatterServices.GetUninitializedObject(type);
            if (!type.IsValueType) done[source] = clone;
            foreach (var field in FieldsOf(type)) field.SetValue(clone, Copy(field.GetValue(source), done));
            return clone;
        }

        /// <summary>Kiểu không chứa tham chiếu nào: chép giá trị là đủ.</summary>
        private static bool IsPlain(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return true;
            if (!type.IsValueType) return false;
            if (Plain.TryGetValue(type, out bool plain)) return plain;
            Plain[type] = true; // chặn đệ quy
            plain = true;
            foreach (var field in FieldsOf(type)) if (!IsPlain(field.FieldType)) { plain = false; break; }
            Plain[type] = plain;
            return plain;
        }

        private static FieldInfo[] FieldsOf(Type type)
        {
            if (Fields.TryGetValue(type, out var fields)) return fields;
            var list = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                list.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            return Fields[type] = list.ToArray();
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}
