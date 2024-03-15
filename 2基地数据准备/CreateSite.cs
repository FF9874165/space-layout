using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    //创建用地的电池
    public class CreateSite : GH_Component
    {
        public CreateSite()
          : base("用地生成", "用地",
              "建设用地基础信息生成",
               "建筑空间布局", "用地生成")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("用地边界", "建设用地红线", "本项目建设用地红线", GH_ParamAccess.item);
            pManager.AddCurveParameter("用地退线", "建筑控制线", "本项目建设用地退红线", GH_ParamAccess.item);
            pManager.AddNumberParameter("建筑密度", "建筑密度", "用于计算最大建筑基底面积", GH_ParamAccess.item);
            pManager.AddNumberParameter("容积率", "容积率", "用于计算最大地上建筑面积", GH_ParamAccess.item);
            pManager.AddNumberParameter("建筑限高", "最大建筑高度", "建筑最大高度", GH_ParamAccess.item);
            pManager.AddNumberParameter("最大地上建筑面积", "地上总建筑面积", "最大地上建筑面积，用于预估建筑层数、大厅数量、指标对比等", GH_ParamAccess.item);
            pManager.AddNumberParameter("上层偏移距离", "上层建筑最大出挑距离", "位于上层的场馆相对于下层允许出挑的最大距离", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //建设用地边界
            GH_Curve site = new GH_Curve();
            GH_Curve siteRetreat = new GH_Curve();
            double buildingDensity = 0.4;
            double plotRatio = 1;
            double height = 30;
            double areaMax = 32000;
            double offset = 8;//偏移距离

            if (!DA.GetData(0, ref site)) return;
            if (!DA.GetData(1, ref siteRetreat)) return;
            if (!DA.GetData(2, ref buildingDensity)) return;
            if (!DA.GetData(3, ref plotRatio)) return;
            if (!DA.GetData(4, ref height)) return;
            if (!DA.GetData(5, ref areaMax)) return;
            if (!DA.GetData(6, ref offset)) return;
            //同步数据
            StaticObject.offset = offset;
            SiteInfo.siteRetreat = siteRetreat;

            //向基地静态管理类同步信息
            SiteInfo.GetInfo(site, buildingDensity, plotRatio, height, areaMax);
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources._1_用地;

        public override Guid ComponentGuid
        {
            get { return new Guid("175528A5-740D-4807-9C0E-D5240EC486E8"); }
        }
    }
}