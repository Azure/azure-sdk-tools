using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewDefinitions : List<Definition>, INavigable
{
    public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        NavigationItem ret = new NavigationItem() {Text = "Definitions"};
        List<NavigationItem> children = new List<NavigationItem>();

        return ret;
    }
}
