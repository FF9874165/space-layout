using System;
using System.Collections.Generic;
using Eto.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;


namespace Space_Layout
{
    public class ChangeBGColor : GH_Component
    {
        public ChangeBGColor()
          : base("ChangeBGColor", "背景色",
              "修改背景板颜色",
              "建筑空间布局", "其他")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("是否更改背景颜色", "启用？", "按钮为true则更改背景色", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //为true则更改背景色
            bool isChange = false;
            if (!DA.GetData(0, ref isChange)) return;

            if (isChange)
            {
                //背景改为白色
                Grasshopper.GUI.Canvas.GH_Skin.canvas_back = System.Drawing.Color.FromArgb(255, 255, 255);
                Grasshopper.GUI.Canvas.GH_Skin.canvas_edge = System.Drawing.Color.FromArgb(255, 255, 255);
                Grasshopper.GUI.Canvas.GH_Skin.canvas_grid = System.Drawing.Color.FromArgb(255, 255, 255);
                Grasshopper.GUI.Canvas.GH_Skin.canvas_shade = System.Drawing.Color.FromArgb(255, 255, 255);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("D1B02404-CCE7-4D56-BA65-5C92730D816A"); }
        }
    }
}