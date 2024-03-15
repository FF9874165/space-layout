using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using NPOI.SS.Formula.Functions;
using Rhino.Display;
using Rhino.Geometry;

namespace Space_Layout
{
    public class CreateAquaticBuilding : GH_Component
    {
        //创建游泳馆建筑单体的电池
        public CreateAquaticBuilding()
          : base("创建游泳馆", "游泳馆",
              "初始化游泳馆信息，生成泳池及其附属用房初始排布",
              "建筑空间布局", "数据预处理")
        {
        }
        //参数输入
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("是否设置此馆", "是否设置", "本项目是否包含此馆", GH_ParamAccess.item);
            pManager.AddNumberParameter("场馆面积", "场馆总建筑面积", "此类场地合计面积", GH_ParamAccess.item);
            pManager.AddIntegerParameter("标准泳池数量", "标准泳池数量", "标准泳池数量，若＞1，则分馆设置", GH_ParamAccess.item);
            pManager.AddIntegerParameter("标准泳池泳道数量", "标准泳池泳道数量", "标准泳池泳道数量，用于计算标准泳池宽度", GH_ParamAccess.list);
            pManager.AddIntegerParameter("非标泳池数量", "非标泳池数量", "非标泳池数量，不分馆", GH_ParamAccess.item);
            pManager.AddIntegerParameter("非标泳池泳道数量", "非标泳池泳道数量", "非标泳池泳道数量，用于计算标准泳池宽度", GH_ParamAccess.item);
            pManager.AddIntegerParameter("儿童戏水池数量", "儿童戏水池数量", "儿童戏水池数量，不分馆", GH_ParamAccess.item);
            pManager.AddNumberParameter("球场划分率", "运动场地与辅助用房面积比", "此类球场将辅助用房划分一部分给运动场地的比例", GH_ParamAccess.item);
            pManager.AddPointParameter("陈列基点", "展示基点", "此类球场陈列基点", GH_ParamAccess.item);
        }
        //参数输出
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("场馆边界", "场馆单体", "场馆单体的空间范围", GH_ParamAccess.list);
            pManager.AddRectangleParameter("泳池边界", "泳池", "泳池的空间范围", GH_ParamAccess.list);
            pManager.AddBoxParameter("辅助用房", "辅助用房", "辅助用房的空间范围", GH_ParamAccess.list);
            pManager.AddGenericParameter("游泳馆类", "游泳馆", "本项目中包括的游泳馆类型", GH_ParamAccess.item);
        }
        //主进程
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region 数据准备
            //变量声明
            bool isValid = false;
            double area = 0;
            int standardPoolCount = 0;
            List<int> standardPoolLaneCount = new List<int>();
            int nonStandardPoolCount = 0;
            int nonStandardPoolLaneCount = 8;
            int childrenPoolCount = 0;
            double reductionRatio = 0;
            Point3d origin = new Point3d();

            //获取输入端数据
            if (!DA.GetData(0, ref isValid)) return;
            if (!DA.GetData(1, ref area)) return;
            if (!DA.GetData(2, ref standardPoolCount)) return;
            if (!DA.GetDataList(3, standardPoolLaneCount)) return;
            if (!DA.GetData(4, ref nonStandardPoolCount)) return;
            if (!DA.GetData(5, ref nonStandardPoolLaneCount)) return;
            if (!DA.GetData(6, ref childrenPoolCount)) return;
            if (!DA.GetData(7, ref reductionRatio)) return;
            if (!DA.GetData(8, ref origin)) return;
            #endregion

            if (isValid)//若本项目设置此类场馆
            {
                //注册函数到重新生成的事件
                Manager.restartEvent += Restart;

                //声明场馆所需变量
                AquaticBuilding aquaticBuilding = new AquaticBuilding(standardPoolCount, standardPoolLaneCount, nonStandardPoolCount, nonStandardPoolLaneCount, childrenPoolCount, area, reductionRatio, origin);
                List<GH_Box> building = new List<GH_Box>();
                List<GH_Rectangle> pools = new List<GH_Rectangle>();
                List<GH_Box> auxiliary = new List<GH_Box>();

                //添加标准泳池
                for (int i = 0; i < standardPoolCount; i++)
                {
                    pools.Add(aquaticBuilding.aquaticBuildingGroup[i].standardPool.swimmingPool);
                }
                //添加非标准泳池
                for (int i = 0; i < nonStandardPoolCount; i++)
                {
                    pools.Add(aquaticBuilding.aquaticBuildingGroup[0].nonStandardPool.swimmingPool);
                }
                //添加儿童池
                for (int i = 0; i < childrenPoolCount; i++)
                {
                    pools.Add(aquaticBuilding.aquaticBuildingGroup[0].childrenPools[i].swimmingPool);
                }
                //添加辅助用房
                for (int i = 0; i < aquaticBuilding.aquaticBuildingGroup.Count; i++)
                {
                    building.Add(aquaticBuilding.aquaticBuildingGroup[i].groupBoundary);
                    for (int j = 0; j < aquaticBuilding.aquaticBuildingGroup[i].aquaticBuildingAuxiliaryGroup.auxiliary.Count; j++)
                    {
                        auxiliary.Add(aquaticBuilding.aquaticBuildingGroup[i].aquaticBuildingAuxiliaryGroup.auxiliary[j].auxiliaryUnit);
                    }
                }

                //数据输出
                DA.SetDataList(0, building);
                DA.SetDataList(1, pools);
                DA.SetDataList(2, auxiliary);
                DA.SetData(3, aquaticBuilding);

                ////赋值给类内变量
                //buildingColor = building;
                //poolsColor = pools;
                //auxiliaryColor = auxiliary;
            }
        }

        //#region 变量
        //List<GH_Box> buildingColor = new List<GH_Box>();
        //List<GH_Rectangle> poolsColor = new List<GH_Rectangle>();
        //List<GH_Box> auxiliaryColor = new List<GH_Box>();
        //#endregion

        //Manager监听的方法，当本轮生成结束后调用
        public void Restart()
        {
            OnPingDocument().ScheduleSolution(StaticObject.timeSpan, new GH_Document.GH_ScheduleDelegate(this.ScheduleCallback));
        }
        //重启下一轮计算的方法
        private void ScheduleCallback(GH_Document doc)
        {
            ExpireSolution(false);
        }
        //public override void DrawViewportMeshes(IGH_PreviewArgs args)
        //{
        //    DisplayMaterial material = new DisplayMaterial();
        //    material = new DisplayMaterial(System.Drawing.Color.PowderBlue);
        //    material.Transparency = 0.4;
        //    if (buildingColor.Count != 0)
        //    {
        //        foreach (GH_Box box in buildingColor)
        //        {
        //            args.Display.DrawBrepShaded(box.Brep(), material);
        //        }
        //    }
        //    if (auxiliaryColor.Count != 0)
        //    {
        //        foreach (GH_Box box in auxiliaryColor)
        //        {
        //            args.Display.DrawBrepShaded(box.Brep(), material);
        //        }
        //    }
        //}
        //public override void DrawViewportWires(IGH_PreviewArgs args)
        //{
        //    DisplayMaterial material = new DisplayMaterial();
        //    if (poolsColor.Count != 0)
        //    {
        //        foreach (GH_Rectangle rect in poolsColor)
        //        {
        //            args.Display.DrawCurve (rect.Value.ToNurbsCurve(), System.Drawing.Color.Red);
        //        }
        //    }
        //}
        protected override System.Drawing.Bitmap Icon => Properties.Resources._6_游泳馆;

        public override Guid ComponentGuid
        {
            get { return new Guid("72FBBB14-8024-4656-997C-D93CA5B091DF"); }
        }
    }
}