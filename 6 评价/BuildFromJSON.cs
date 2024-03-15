using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Eto.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Utilities;
using Rhino.Display;
using Rhino.Geometry;
using Rhino;

namespace Space_Layout
{
    //由JSON构建实体并展示
    public class BuildFromJSON : GH_Component
    {
        public BuildFromJSON()
          : base("成果展示", "展示",
              "读入指定的简化形体JSON文件，用以重构模型，并展示",
              "建筑空间布局", "方案评价")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("是否开始", "开始", "是否开始读入指定的简化形体JSON文件，用以重构模型，并展示", GH_ParamAccess.item);
            pManager.AddPointParameter("展示基点", "基点", "展示手工筛选出的方案，平面排列的基准点", GH_ParamAccess.item);
            pManager.AddIntegerParameter("拷贝编号", "编号", "要拷贝出来的方案编号", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("场馆形体", "场馆", "", GH_ParamAccess.list);
            pManager.AddCurveParameter("用地退线边界", "边界", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region 数据准备
            bool isBegin = false;//生成状态，用于进程管理
            Point3d pivot = Point3d.Origin;
            int bakeNumber = -1;//要拷贝出来的方案编号

            if (!DA.GetData(0, ref isBegin)) return;
            if (!DA.GetData(1, ref pivot)) return;
            if (!DA.GetData(2, ref bakeNumber)) return;
            #endregion


            if (isBegin)//若开始了
            {
                #region 从json写入
                //构建文件路径
                string filePath = "C:\\Users\\Administrator\\Desktop\\SpaceLayout";
                //string filePath = "C:\\Users\\v3-sx8\\Desktop\\SpaceLayout";
                //文件夹目录是否存在，若不存在则新建一个
                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }
                //获取文件夹中的文件名称
                string[] names = Directory.GetFiles(filePath);
                //读入JSON文件
                List<string> inputJsons = new List<string>();
                simplifiedBuildings = new List<List<SimplifiedBuilding>>();
                boundaries = new List<Curve>();
                if (names.Length != 0)
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        //读入JSON文件
                        inputJsons.Add(File.ReadAllText(names[i]));
                        //实例化场馆
                        simplifiedBuildings.Add(JsonConvert.DeserializeObject<List<SimplifiedBuilding>>(inputJsons[i]));
                        //移动到指定位置
                        for (int j = 0; j < simplifiedBuildings[i].Count; j++)
                        {
                            simplifiedBuildings[i][j].MoveToPoint(i, pivot);
                        }
                        //构建用地边界的排布
                        boundaries.Add(BoundaryLayout(i, pivot));
                    }
                }
                #endregion

                #region 场馆展示
                List<GH_Box> tempBox = new List<GH_Box>();
                List<Curve> tempCurve = new List<Curve>();
                for (int i = 0; i < simplifiedBuildings.Count; i++)
                {
                    if (simplifiedBuildings[i] != null)
                    {
                        for (int j = 0; j < simplifiedBuildings[i].Count; j++)
                        {
                            if (simplifiedBuildings[i][j] != null)
                            {
                                tempBox.Add(simplifiedBuildings[i][j].building);
                                tempCurve.Add(boundaries[i]);
                            }
                        }
                    }
                }
                #endregion

                #region BAKE
                if ((bakeNumber!=-1)&&(bakeNumber>=0) &&(bakeNumber< simplifiedBuildings.Count))//若输入拷贝的方案编号有效
                {
                    if (ifFirstTimeBake)//若为首次BAKE
                    {
                        ifFirstTimeBake = false;
                        lastTimeIndex = bakeNumber;//记载本次拷贝的对象
                        BakeObject(bakeNumber);//BAKE对象
                    }
                    else//非首次
                    {
                        if (lastTimeIndex!= bakeNumber)//若输入端变了再bake，不要重复多次bake
                        {
                            lastTimeIndex = bakeNumber;//记载本次拷贝的对象
                            BakeObject(bakeNumber);//BAKE对象
                        }
                    }
                }
                #endregion

                DA.SetDataList(0, tempBox);
                DA.SetDataList(1, boundaries);
            }
        }
        //简化场馆的对象
        List<List<SimplifiedBuilding>> simplifiedBuildings;
        List<Curve> boundaries;
        bool ifFirstTimeBake=true;//控制拷贝的执行，相同的对象执行一次就好
        int lastTimeIndex=-1;//上次拷贝的编号

        //绘制用地边界排布
        public Curve BoundaryLayout(int num, Point3d inputPivot)
        {
            //获取用地曲线与退线
            Curve boundaryRetreat = SiteInfo.siteRetreat.Value.DuplicateCurve();

            //方案间隔的距离
            double width = Math.Round(SiteInfo.siteBoundary.Value.GetBoundingBox(false).Max.X - SiteInfo.siteBoundary.Value.GetBoundingBox(false).Min.X);
            double length = Math.Round(SiteInfo.siteBoundary.Value.GetBoundingBox(false).Max.Y - SiteInfo.siteBoundary.Value.GetBoundingBox(false).Min.Y);
            width += 100;
            length += 100;
            //方案移动变量
            Vector3d move;
            Vector3d dist = inputPivot - SiteInfo.siteBoundary.Value.GetBoundingBox(false).Center;

            //求移动变量
            if (num < StaticObject.countPerRow) //若智能排满一行
            {
                move = new Vector3d(width * num, 0, 0) + dist;
            }
            else//排一行以上
            {
                move = new Vector3d(width * (num % StaticObject.countPerRow), length * (num / StaticObject.countPerRow), 0) + dist;
            }
            //移动曲线
            boundaryRetreat.Transform(Transform.Translation(move));
            return boundaryRetreat;
        }
        //更新绘制区域的边界
        public override BoundingBox ClippingBox
        {
            get
            {
                if (boundaries!=null)//若进行了方案排布
                {
                    //获取排布方案区域的总boundingBox
                    BoundingBox tempBox= boundaries[0].GetBoundingBox(false);
                    for (int i = 0; i < boundaries.Count; i++)
                    {
                        tempBox.Union(boundaries[i].GetBoundingBox(false));
                    }

                    return tempBox;

                }
                else
                {
                    return BoundingBox.Empty;
                }
            }
        }
        //重绘显示线(辅助用房、场馆)
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            //绘制基地退线的排布
            if (boundaries != null)
            {
                for (int i = 0; i < boundaries.Count; i++)
                {
                    args.Display.DrawCurve(boundaries[i], System.Drawing.Color.Black, 1);
                }
            }
        }
        //重绘显示网格
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (simplifiedBuildings != null)
            {
                #region 绘制场馆
                DisplayMaterial material = new DisplayMaterial();
                for (int i = 0; i < simplifiedBuildings.Count; i++)
                {
                    if (simplifiedBuildings[i] != null)
                    {
                        for (int j = 0; j < simplifiedBuildings[i].Count; j++)
                        {
                            if (simplifiedBuildings[i][j] != null)
                            {
                                #region 绘制线框
                                args.Display.DrawBrepWires(simplifiedBuildings[i][j].building.Value.ToBrep(), System.Drawing.Color.Black);
                                for (int k = 0; k < simplifiedBuildings[i][j].gymAuxiliaries.Count; k++)
                                {
                                    args.Display.DrawBrepWires(simplifiedBuildings[i][j].gymAuxiliaries[k].Value.ToBrep(), System.Drawing.Color.Black);
                                }
                                for (int k = 0; k < simplifiedBuildings[i][j].courts.Count; k++)
                                {
                                    args.Display.DrawPolyline(simplifiedBuildings[i][j].courts[k].Value.ToPolyline(), System.Drawing.Color.Black, 1);
                                }
                                #endregion

                                #region 绘制着色场馆体量
                                switch (simplifiedBuildings[i][j].name)
                                {
                                    case BuildingType.篮球比赛馆:
                                        material = new DisplayMaterial(System.Drawing.Color.LightCyan);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.篮球训练馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Orchid);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.游泳馆:
                                        material = new DisplayMaterial(System.Drawing.Color.PowderBlue);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.羽毛球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.GreenYellow);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.网球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.LimeGreen);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.冰球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.LightSkyBlue);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.乒乓球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Yellow);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.健身馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Pink);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.办公:
                                        material = new DisplayMaterial(System.Drawing.Color.Brown);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.观演厅:
                                        material = new DisplayMaterial(System.Drawing.Color.Purple);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.大厅:
                                        material = new DisplayMaterial(System.Drawing.Color.White);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    case BuildingType.其他:
                                        material = new DisplayMaterial(System.Drawing.Color.Gray);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(simplifiedBuildings[i][j].building.Boundingbox), material);
                                        break;
                                    default:
                                        break;
                                }
                                #endregion
                            }
                        }
                    }

                }
                #endregion

                #region 写文字
                for (int i = 0; i < boundaries.Count; i++)
                {
                    Point3d pivot = boundaries[i].GetBoundingBox(false).Min;
                    pivot.Transform(Transform.Translation(new Vector3d(0, -50, 0)));
                    args.Display.Draw2dText("方案" + (i+1).ToString(), System.Drawing.Color.Black, pivot, false, 40);
                }
                #endregion
            }
        }
        //获取颜色
        public System.Drawing.Color GetColor(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.篮球比赛馆:
                    return System.Drawing.Color.Purple;
                case BuildingType.篮球训练馆:
                    return System.Drawing.Color.Orchid;
                case BuildingType.游泳馆:
                    return System.Drawing.Color.PowderBlue;
                case BuildingType.羽毛球馆:
                    return System.Drawing.Color.GreenYellow;
                case BuildingType.网球馆:
                    return System.Drawing.Color.LimeGreen;
                case BuildingType.冰球馆:
                    return System.Drawing.Color.LightSkyBlue;
                case BuildingType.乒乓球馆:
                    return System.Drawing.Color.Yellow;
                case BuildingType.健身馆:
                    return System.Drawing.Color.Pink;
                case BuildingType.办公:
                    return System.Drawing.Color.Brown;
                case BuildingType.观演厅:
                    return System.Drawing.Color.Purple;
                case BuildingType.大厅:
                    return System.Drawing.Color.White;
                case BuildingType.其他:
                    return System.Drawing.Color.Gray;
                default:
                    return System.Drawing.Color.White;
            }
        }
        //BAKE
        public void BakeObject(int number)
        {
            for (int i = 0; i < simplifiedBuildings[number].Count; i++)
            {
                if (simplifiedBuildings[number][i]!=null)
                {
                    Rhino.DocObjects.ObjectAttributes att = new Rhino.DocObjects.ObjectAttributes();
                    att.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject;
                    att.ObjectColor = GetColor(simplifiedBuildings[number][i].name); //导入犀牛后的颜色
                    att.LayerIndex = 0;//图层位置
                    //bake场馆体量
                    RhinoDoc.ActiveDoc.Objects.AddBox(simplifiedBuildings[number][i].building.Value, att); 
                    //bake辅助用房
                    if (simplifiedBuildings[number][i].gymAuxiliaries!=null)
                    {
                        att.LayerIndex = 1;//图层位置
                        for (int j = 0; j < simplifiedBuildings[number][i].gymAuxiliaries.Count; j++)
                        {
                            RhinoDoc.ActiveDoc.Objects.AddBox(simplifiedBuildings[number][i].gymAuxiliaries[j].Value, att);
                        }
                    }
                    //bake运动场地
                    if (simplifiedBuildings[number][i].courts != null)
                    {
                        att.LayerIndex = 2;//图层位置
                        for (int j = 0; j < simplifiedBuildings[number][i].courts.Count; j++)
                        {
                            RhinoDoc.ActiveDoc.Objects.AddRectangle(simplifiedBuildings[number][i].courts[j].Value, att);
                        }
                    }
                    //bake用地退线
                    att.LayerIndex = 3;//图层位置
                    RhinoDoc.ActiveDoc.Objects.AddCurve(boundaries[number], att);
                }
            }
        }
        protected override System.Drawing.Bitmap Icon => Properties.Resources._14_写入JSON;

        public override Guid ComponentGuid
        {
            get { return new Guid("8B9C32A0-5605-4A08-BB60-90B953FDC07D"); }
        }
    }
}