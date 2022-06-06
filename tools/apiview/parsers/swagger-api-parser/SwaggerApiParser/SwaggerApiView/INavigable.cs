using System;
using System.Collections.Generic;
using APIView;


namespace SwaggerApiParser
{
    public interface INavigable
    {
        public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null);
    }
}
