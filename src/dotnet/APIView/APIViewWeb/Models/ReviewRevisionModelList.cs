// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class ReviewRevisionModelList : IList<ReviewRevisionModel>
    {
        private readonly ReviewModel _review;
        private readonly List<ReviewRevisionModel> _list;
        public ReviewRevisionModelList(ReviewModel review)
        {
            _review = review;
            _list = new List<ReviewRevisionModel>();
        }

        public ReviewRevisionModel this[int index] { get => _list[index]; set => _list[index] = value; }

        public int Count => _list.Count;

        public bool IsReadOnly => ((IList<ReviewRevisionModel>)_list).IsReadOnly;

        public void Add(ReviewRevisionModel item)
        {
            item.Review = _review;
            _list.Add(item);
        }

        public void AddRange(IEnumerable<ReviewRevisionModel> revisionModels)
        {
            foreach (ReviewRevisionModel revision in revisionModels)
            {
                Add(revision);
            }
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(ReviewRevisionModel item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(ReviewRevisionModel[] array, int arrayIndex)
        {
           _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<ReviewRevisionModel> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int IndexOf(ReviewRevisionModel item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, ReviewRevisionModel item)
        {
            _list.Insert(index, item);
        }

        public bool Remove(ReviewRevisionModel item)
        {
            return _list.Remove(item);
        }

        public void RemoveAt(int index)
        {
           _list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }
}
