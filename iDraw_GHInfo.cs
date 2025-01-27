using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace iDraw_GH
{
    public class iDraw_GHInfo : GH_AssemblyInfo
    {
        public override string Name => "iDraw_GH";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("821B9CFB-907A-480D-9F70-F149074DF2A4");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}