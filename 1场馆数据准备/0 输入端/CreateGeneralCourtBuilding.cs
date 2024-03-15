using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    public class CreateGeneralCourtBuilding : GH_Component
    {
        public CreateGeneralCourtBuilding()
          : base("创建通用类球馆", "通用球馆",
              "初始化通用类球馆信息（包括：篮球训练馆、羽毛球馆、网球馆、冰球馆、乒乓球馆），生成通用球场地及其附属用房初始排布",
              "建筑空间布局", "数据预处理")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("场馆名称", "场馆名称", "场馆根据功能类型划分的名称", GH_ParamAccess.item);
            pManager.AddBooleanParameter("是否设置此馆", "是否设置", "本项目是否包含此馆", GH_ParamAccess.item);
            pManager.AddIntegerParameter("场地数量", "运动场地数量", "此类场地共计片数", GH_ParamAccess.item);
            pManager.AddNumberParameter("场馆面积", "场馆总建筑面积", "此类场地合计面积", GH_ParamAccess.item);
            pManager.AddBooleanParameter("是否设置专属辅助用房", "是否设置独立辅助用房", "此类场地是否是设置专属辅助用房的独立场馆；或与其他馆公用附属用房", GH_ParamAccess.item);
            pManager.AddNumberParameter("球场划分率", "运动场地与辅助用房面积比", "此类球场将辅助用房划分一部分给运动场地的比例", GH_ParamAccess.item);
            pManager.AddPointParameter("陈列基点", "展示基点", "此类球场陈列基点", GH_ParamAccess.item);
            pManager.AddBooleanParameter("是否多功能", "是否多功能", "次场馆是否用于多功能使用", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("场馆边界", "场馆单体", "场馆单体的空间范围", GH_ParamAccess.list);
            pManager.AddRectangleParameter("球场", "运动场地", "运动场地的空间范围", GH_ParamAccess.list);
            pManager.AddBoxParameter("辅助用房", "辅助用房", "辅助用房的空间范围", GH_ParamAccess.list);
            pManager.AddGenericParameter("建筑场馆类", "场馆", "本项目中包括的建筑场馆类型", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //注册函数到重新生成的事件+
            Manager.restartEvent += Restart;

            //数据初始化
            string name = null;
            bool IsValid = false;
            int count = 0;
            double area = 0;
            bool hasAuxiliary = false;
            double reductionRatio = 0;
            Point3d origin = new Point3d();
            bool isMultifuction = false;

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref IsValid)) return;
            if (!DA.GetData(2, ref count)) return;
            if (!DA.GetData(3, ref area)) return;
            if (!DA.GetData(4, ref hasAuxiliary)) return;
            if (!DA.GetData(5, ref reductionRatio)) return;
            if (!DA.GetData(6, ref origin)) return;
            if (!DA.GetData(7, ref isMultifuction)) return;


            if (IsValid)
            {
                //获取1个乒乓球馆，进行该类场馆总体数据管理、行为协调
                GeneralCourtBuilding generalCourtBuilding = new GeneralCourtBuilding(name, IsValid, count, area, hasAuxiliary, reductionRatio, origin, isMultifuction);
                //获取所有场馆组
                generalCourtBuilding.CreateSportsBuildingGroup();

                List<GH_Box> buildingBoundary = new List<GH_Box>();
                List<GH_Rectangle> court = new List<GH_Rectangle>();
                List<GH_Box> auxiliary = new List<GH_Box>();

                for (int i = 0; i < generalCourtBuilding.generalCourtBuildingGroups.Count; i++)
                {
                    buildingBoundary.Add(generalCourtBuilding.generalCourtBuildingGroups[i].groupBoundary);
                    for (int k = 0; k < generalCourtBuilding.generalCourtBuildingGroups[i].generalCourtGroup.courtOutline.Count; k++)
                    {
                        court.Add(generalCourtBuilding.generalCourtBuildingGroups[i].generalCourtGroup.courtOutline[k]);
                    }
                    if (hasAuxiliary)
                    {
                        for (int j = 0; j < generalCourtBuilding.generalCourtBuildingGroups[i].generalCourtAuxiliaryGroup.auxiliary.Count; j++)
                        {
                            auxiliary.Add(generalCourtBuilding.generalCourtBuildingGroups[i].generalCourtAuxiliaryGroup.auxiliary[j].auxiliaryUnit);
                        }
                    }
                }

                DA.SetDataList(0, buildingBoundary);
                DA.SetDataList(1, court);
                DA.SetDataList(2, auxiliary);
                DA.SetData(3, generalCourtBuilding);
            }
        }
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
        protected override System.Drawing.Bitmap Icon => Properties.Resources._5_球类运动;

        public override Guid ComponentGuid
        {
            get { return new Guid("B716D3AA-8535-4243-A315-AD5D7EE99E8E"); }
        }
    }
}