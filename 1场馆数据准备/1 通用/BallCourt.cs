using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //记录该类运动场地特性数据、总体数据
    public class BallCourt : Court
    {
        public bool multiFunctional = false;//针对大型活动，一般为篮球馆
        public int count;//场地总片数

        //拆分场地
        public int courtGroupNumber = 1;//运动场地组数量，对应场馆单体数量
        public double splitRatio = 1;//运动场地切分率
        public List<int> courtNumberPerGroup = new List<int>();

        //构造函数
        public BallCourt(CourtType courtType)
        {
            this.courtType = courtType;
            switch (courtType)
            {
                #region"篮球比赛场"
                case CourtType.篮球比赛场:
                    lengthPerCourt = 28.2;
                    widthPerCourt = 15.2;
                    if (multiFunctional == true) clearHeight = 15;
                    else clearHeight = 7;
                    terminalDistance = 5;
                    sidelineDistance = 6;
                    terminalSpacing = 0;
                    sidelineSpacing = 0;
                    break;
                #endregion

                #region"篮球训练场"
                case CourtType.篮球训练场:
                    lengthPerCourt = 28.2;
                    widthPerCourt = 15.2;
                    if (multiFunctional == true) clearHeight = 15;
                    else clearHeight = 7;

                    terminalDistance = 2;
                    sidelineDistance = 2;
                    terminalSpacing = 2;
                    sidelineSpacing = 2;
                    break;
                #endregion

                #region "羽毛球"
                case CourtType.羽毛球场:
                    lengthPerCourt = 13.4;
                    widthPerCourt = 6.1;
                    clearHeight = 7;

                    terminalDistance = 2;
                    sidelineDistance = 2;
                    terminalSpacing = 2;
                    sidelineSpacing = 2;
                    break;
                #endregion

                #region"网球场"
                case CourtType.网球场:
                    lengthPerCourt = 23.77;
                    widthPerCourt = 10.79;
                    clearHeight = 8;

                    terminalDistance = 6.4;
                    sidelineDistance = 3.66;
                    terminalSpacing = 6.4;
                    sidelineSpacing = 5;
                    break;
                #endregion

                #region"冰球场"
                case CourtType.冰球场:
                    lengthPerCourt = 60;
                    widthPerCourt = 30;
                    clearHeight = 6;

                    terminalDistance = 3;
                    sidelineDistance = 2;
                    terminalSpacing = 3;
                    sidelineSpacing = 2;
                    break;
                #endregion

                #region"乒乓球场"
                case CourtType.乒乓球场:
                    lengthPerCourt = 2.74;
                    widthPerCourt = 1.53;
                    clearHeight = 4;

                    terminalDistance = 2.5;
                    sidelineDistance = 2;
                    terminalSpacing = 5;
                    sidelineSpacing = 2;
                    break;
                    #endregion
            }
        }
        // 是否在球场排布前进行体量切割
        public void SplitBallCourt(int count)
        {
            this.count = count;
            switch (this.courtType)
            {
                #region"篮球比赛场"
                case CourtType.篮球比赛场:
                    //篮球一般兼做多功能场地，空间需求大，一般不拆分
                    courtGroupNumber = 1;
                    courtNumberPerGroup.Add(count);
                    break;
                #endregion

                #region"篮球训练场"
                case CourtType.篮球训练场:
                    //篮球一般兼做多功能场地，空间需求大，一般不拆分
                    courtGroupNumber = 1;
                    courtNumberPerGroup.Add(count);
                    break;
                #endregion

                #region "羽毛球"
                case CourtType.羽毛球场:
                    //？？？拆分边界
                    if ((count > 4) && (Tool.GetRatio() > 0.5))
                    {
                        courtGroupNumber = 2;
                        //计算每组的场地数，包括不均匀拆分
                        splitRatio = Tool.GetSplitRatio();
                        int badmintonNumber = (int)Math.Round(count * splitRatio, 0);
                        courtNumberPerGroup.Add(badmintonNumber);
                        courtNumberPerGroup.Add(count - badmintonNumber);
                        if (badmintonNumber == (count - badmintonNumber))//2组当球场数相等时，更新拆分率
                        {
                            splitRatio = 0.5;
                        }
                    }
                    else
                    {
                        courtGroupNumber = 1;
                        courtNumberPerGroup.Add(count);
                    }
                    break;
                #endregion

                #region"网球场"
                case CourtType.网球场:
                    //？？？拆分边界
                    if ((count > 2) && (Tool.GetRatio() > 0.5))
                    {
                        courtGroupNumber = 2;
                        //计算每组的场地数，包括不均匀拆分
                        splitRatio = Tool.GetSplitRatio();
                        int tennisNumber = (int)Math.Round(count * splitRatio, 0);
                        courtNumberPerGroup.Add(tennisNumber);
                        courtNumberPerGroup.Add(count - tennisNumber);
                        if (tennisNumber == (count - tennisNumber))//2组当球场数相等时，更新拆分率
                        {
                            splitRatio = 0.5;
                        }
                    }
                    else
                    {
                        courtGroupNumber = 1;
                        courtNumberPerGroup.Add(count);
                    }
                    break;
                #endregion

                #region"冰球场"
                case CourtType.冰球场:
                    //冰球场一个项目最多就一块，不拆分
                    courtGroupNumber = 1;
                    courtNumberPerGroup.Add(count);
                    break;
                #endregion

                #region"乒乓球场"
                case CourtType.乒乓球场:
                    //？？？拆分边界
                    if ((count > 8) && (Tool.GetRatio() > 0.7))
                    {
                        courtGroupNumber = 2;
                        //计算每组的场地数，包括不均匀拆分
                        splitRatio = Tool.GetSplitRatio();
                        int pingPongNumber = (int)Math.Round(count * splitRatio, 0);
                        courtNumberPerGroup.Add(pingPongNumber);
                        courtNumberPerGroup.Add(count - pingPongNumber);
                        if (pingPongNumber == (count - pingPongNumber))//2组当球场数相等时，更新拆分率
                        {
                            splitRatio = 0.5;
                        }
                    }
                    else
                    {
                        courtGroupNumber = 1;
                        courtNumberPerGroup.Add(count);
                    }
                    break;
                    #endregion
            }

        }
    }
}
