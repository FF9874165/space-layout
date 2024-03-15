using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace Space_Layout
{
    public class Space_LayoutInfo : GH_AssemblyInfo
    {
        public override string Name => "SpaceLayout";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "用于全民健身类体育场馆，或同级别文体场馆的概念方案体量排布";

        public override Guid Id => new Guid("1b298993-c02f-41cc-9200-bca822d668f2");

        //Return a string identifying you or your company.
        public override string AuthorName => "Almond";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "almond2323@126.com";
    }
}