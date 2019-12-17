using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public ReviewRevisionModel this[int index] { get => ((IList<ReviewRevisionModel>)this._list)[index]; set => ((IList<ReviewRevisionModel>)this._list)[index] = value; }

        public int Count => ((IList<ReviewRevisionModel>)this._list).Count;

        public bool IsReadOnly => ((IList<ReviewRevisionModel>)this._list).IsReadOnly;

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
            ((IList<ReviewRevisionModel>)this._list).Clear();
        }

        public bool Contains(ReviewRevisionModel item)
        {
            return ((IList<ReviewRevisionModel>)this._list).Contains(item);
        }

        public void CopyTo(ReviewRevisionModel[] array, int arrayIndex)
        {
            ((IList<ReviewRevisionModel>)this._list).CopyTo(array, arrayIndex);
        }

        public IEnumerator<ReviewRevisionModel> GetEnumerator()
        {
            return ((IList<ReviewRevisionModel>)this._list).GetEnumerator();
        }

        public int IndexOf(ReviewRevisionModel item)
        {
            return ((IList<ReviewRevisionModel>)this._list).IndexOf(item);
        }

        public void Insert(int index, ReviewRevisionModel item)
        {
            ((IList<ReviewRevisionModel>)this._list).Insert(index, item);
        }

        public bool Remove(ReviewRevisionModel item)
        {
            return ((IList<ReviewRevisionModel>)this._list).Remove(item);
        }

        public void RemoveAt(int index)
        {
            ((IList<ReviewRevisionModel>)this._list).RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<ReviewRevisionModel>)this._list).GetEnumerator();
        }
    }
}
