// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Compare
{
    public class ApiComparer
    {
        Assembly BaseAssembly { get; init; }
        Assembly CurAssembly { get; init; }

        public ApiComparer(Assembly curAssembly, Assembly baseAssembly)
        {
            BaseAssembly = baseAssembly;
            CurAssembly = curAssembly;
        }

        public ChangeSet Compare()
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            ChangeSet result = new ChangeSet();

            CompareList(
                CurAssembly.GetTypes(), BaseAssembly.GetTypes(), (t) => t.GetKey(),
                t => result.Changes.Add(new TypeChange(t, ChangeCatogory.Added)),
                t => result.Changes.Add(new TypeChange(t, ChangeCatogory.Removed)),
                (t, typeBaseline) =>
                {
                    if (t.IsObsoleted() && !typeBaseline.IsObsoleted())
                    {
                        result.Changes.Add(new TypeChange(t, ChangeCatogory.Obsoleted));
                    }
                    CompareList(
                        t.GetConstructors(flags), typeBaseline.GetConstructors(flags), c => c.GetKey(),
                        c => result.Changes.Add(new CtorChange(c, ChangeCatogory.Added)),
                        c => result.Changes.Add(new CtorChange(c, ChangeCatogory.Removed)),
                        (c, ctorBaseline) =>
                        {
                            if (c.IsObsoleted() && !ctorBaseline.IsObsoleted())
                            {
                                result.Changes.Add(new CtorChange(c, ChangeCatogory.Obsoleted));
                            }
                        });
                    CompareList(
                        t.GetMethods(flags, includePropertyMethod: false), typeBaseline.GetMethods(flags, includePropertyMethod: false), m => m.GetKey(),
                        m => result.Changes.Add(new MethodChange(m, ChangeCatogory.Added)),
                        m => result.Changes.Add(new MethodChange(m, ChangeCatogory.Removed)),
                        (m, methodBaseline) =>
                        {
                            if (m.IsObsoleted() && !methodBaseline.IsObsoleted())
                            {
                                result.Changes.Add(new MethodChange(m, ChangeCatogory.Obsoleted));
                            }
                        });
                    CompareList(
                        t.GetProperties(flags), typeBaseline.GetProperties(flags), p => p.GetKey(),
                        p => result.Changes.Add(new PropertyChange(p, ChangeCatogory.Added)),
                        p => result.Changes.Add(new PropertyChange(p, ChangeCatogory.Removed)),
                        (p, propertyBaseline) =>
                        {
                            if (p.IsObsoleted() && !propertyBaseline.IsObsoleted())
                            {
                                result.Changes.Add(new PropertyChange(p, ChangeCatogory.Obsoleted));
                            }
                            if(p.CanRead != propertyBaseline.CanRead)
                            {
                                result.Changes.Add(new PropertyMethodChange(p, PropertyMethodName.Get, p.CanRead ? ChangeCatogory.Added : ChangeCatogory.Removed));
                            }
                            else if (p.CanRead)
                            {
                                if(p.GetMethod!.IsObsoleted() && !propertyBaseline.GetMethod!.IsObsoleted())
                                {
                                    result.Changes.Add(new PropertyMethodChange(p, PropertyMethodName.Get, ChangeCatogory.Obsoleted));
                                }
                            }
                            if(p.CanWrite != propertyBaseline.CanWrite)
                            {
                                result.Changes.Add(new PropertyMethodChange(p, PropertyMethodName.Set, p.CanWrite ? ChangeCatogory.Added : ChangeCatogory.Removed));
                            }
                            else if (p.CanWrite)
                            {
                                if(p.SetMethod!.IsObsoleted() && propertyBaseline.SetMethod!.IsObsoleted())
                                {
                                    result.Changes.Add(new PropertyMethodChange(p, PropertyMethodName.Set, ChangeCatogory.Obsoleted));
                                }
                            }
                        });
                });

            return result;
        }

        private static void CompareList<T>(IEnumerable<T> items, IEnumerable<T> baseline, Func<T, string> getKey,
            Action<T> onAdded, Action<T> onRemoved, Action<T, T> onFound) where T : class
        {
            Dictionary<string, T> dict = baseline.ToDictionary(b => getKey(b));
            foreach (var item in items)
            {
                var key = getKey(item);
                if (dict.TryGetValue(key, out T? found))
                {
                    onFound(item, found);
                    dict.Remove(key);
                }
                else
                {
                    onAdded(item);
                }
            }
            foreach (var item in dict.Values)
            {
                onRemoved(item);
            }
        }
    }
}
