using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Drawing.Drawing2D;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.Util;
using System.IO;

namespace RobotArm
{
    public partial class Form1 : Form
    {
        //在PictureBox1的背景图片中作画，在图片中显示机械臂
        private void Form1_Load(object sender, EventArgs e)
        {
            DrawMechanism();
            Bitmap bt = new Bitmap(this.pictureBox1.Width, this.pictureBox1.Height);
            pictureBox1.BackgroundImage = bt;
        }

        //private void numericUpDown_ValueChanged(object sender, EventArgs e)
        //{//调整Canny检测算子的下边缘，得到不同的轮廓
        //    Mat _outputmat = new Mat();
        //    CvInvoke.Canny(imageBox1.Image, _outputmat, Convert.ToInt16(numericUpDown1.Value), Convert.ToInt16(numericUpDown2.Value));
        //    imageBox2.Image = _outputmat;
        //    imageBox2.Refresh();
        //    CvInvoke.FindContours(_outputmat, contours, null, Emgu.CV.CvEnum.RetrType.External,
        //            Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
        //}

        private void button3_Click(object sender, EventArgs e)
        {
            IOutputArray hierarchy = null;//与contours对应的向量，hierarchy[i][0]~hierarchy[i][3]存储后、前、子、父级轮廓链表表头
            OpenFileDialog ofd = new OpenFileDialog();//创建一个对话框，选择需要画轮廓的图片
            ofd.Filter = "JPG图片|*.jpg|BMP图片|*.bmp";//选择文件的类型（filter：过滤）
            
            //处理图像，得到图像的轮廓信息！
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Mat _inputmat = new Mat(ofd.FileName);
                imageBox1.Image = _inputmat;//读入图像，在ImageBox中显示
                CvInvoke.GaussianBlur(_inputmat, _inputmat, new Size(3, 3), 3, 3);//对输入图像进行高斯滤波，并将滤波后的图像存至_inputmat
                Mat dst = new Mat();//存储图片轮廓信息
                CvInvoke.Canny(_inputmat, dst, 120, 180);//Canny 边缘检测算子
                imageBox2.Image = dst;//显示轮廓图片

                #region  CvInvoke.FindContours方法参数讲解
                ///<summary>
                ///IOutputArray contours：检测到的轮廓。通常使用VectorOfVectorOfPoint类型。
                ///IOutputArray hierarchy：可选的输出向量,包含图像的拓扑信息。不使用的时候可以用 null 填充。
                ///每个独立的轮廓（连通域）对应 4 个 hierarchy元素 hierarchy[i][0]~hierarchy[i][4]
                ///（i表示独立轮廓的序数）分别表示后一个轮廓、前一个轮廓、父轮廓、子轮廓的序数。

                ///RetrType mode标识符及其解析：
                ///External = 0 提取的最外层轮廓；
                ///List = 1 提取所有轮廓
                ///Ccomp = 2 检索所有轮廓并将它们组织成两级层次结构:水平是组件的外部边界,二级约束边界的洞。
                ///Tree = 3 提取所有的轮廓和建构完整的层次结构嵌套的轮廓。

                ///ChainApproxMethod表示轮廓的逼近方法
                ///ChainCode = 0 Freeman链码输出轮廓。所有其他方法输出多边形(顶点序列)。
                ///ChainApproxNone = 1 所有的点从链代码转化为点;
                ///ChainApproxSimple = 2 压缩水平、垂直和对角线部分,也就是说, 只剩下他们的终点;
                ///ChainApproxTc89L1 = 3 使用The - Chinl 链逼近算法的一个
                ///ChainApproxTc89Kcos = 4 使用The - Chinl 链逼近算法的一个
                ///LinkRuns = 5, 使用完全不同的轮廓检索算法通过链接的水平段的1s轨道。
                ///用这种方法只能使用列表检索模式。
                ///</summary>
                #endregion

                CvInvoke.FindContours(dst, contours, hierarchy, Emgu.CV.CvEnum.RetrType.External,
                    Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
            }
            graph = "Graph";
        }

        public Form1()
        {
            InitializeComponent();
        }

        #region 全局变量

        private int L1 = 100, L2 = 100;  //用来存储机械臂的两臂长度
        private double Ang1 = 0, Ang2 = 0;  //用来存储机械臂的扭转角度，单位为弧度,取值范围为（-PI,PI）
        double startX;   // 用来存储运动开始的位置
        double startY;
        double endX = 200;//用来存储运动结束的位置
        double endY = 0;
       
        string graph;//用来标志画直线还是画圆
        PointF pointLast;//机械臂末端上一时刻位置
        PointF pointNow;//机械臂末端现在位置
        VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();//创建用于存储轮廓的VectorOfVectorOfPoint数据类型（命名空间Emgu.CV.Util）

        #endregion

        private void timer1_Tick(object sender, EventArgs e)
        {
            //机械臂现在末端位置
            pointNow = new PointF((float)(200 + L1 * Math.Cos(Ang1) + L2 * Math.Cos(Ang1 + Ang2)), (float)(200 - L1 * Math.Sin(Ang1) - L2 * Math.Sin(Ang1 + Ang2)));
            if (pointLast == new PointF(0, 0))
            {
                pointLast = pointNow;//如果是绘制曲线的起点，便没有pointLast，令其就等与pointNow
            }
            Bitmap bt = new Bitmap(pictureBox1.BackgroundImage);//保证了现在绘图是在以前绘图的基础上，不会丢失先前轨迹
            Graphics g = Graphics.FromImage(bt);
            //绘制斜线时消除锯齿
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SmoothingMode = SmoothingMode.HighQuality;
            //将机械臂上一次末端位置及现在末端位置连起来，因为两个位置及近，就是用直线不断拟合轨迹
            g.DrawLine(new Pen(Color.Black, (float)0.4), pointLast, pointNow);
            //释放资源
            g.Dispose();
            //将包含新的轨迹的位图赋给PictureBox1的BackgroundImage
            pictureBox1.BackgroundImage = bt;
            pointLast = pointNow;//更新上一次机械臂末端位置，
        }

        private void button1_Click(object sender, EventArgs e)
        {//“画直线”按钮的点击事件
            groupBox1.Visible = true;//坐标输入功能开启
            groupBox2.Visible = false;//写字输入功能关闭
            textBox4.Visible = true;
            label9.Text = "起点：";
            label10.Text = "终点：";
            graph = "Line";//标志其为画直线
        }

        private void button2_Click(object sender, EventArgs e)
        {//“画圆”按钮的点击事件
            groupBox1.Visible = true;//坐标输入功能开启
            groupBox2.Visible = false;//写字输入功能关闭
            textBox4.Visible = false;
            label9.Text = "圆心：";
            label10.Text = "半径：";
            graph = "Circle";//标志其为画圆
        }
        private void button4_Click(object sender, EventArgs e)
        {
            groupBox1.Visible = false;
            groupBox2.Visible = true;
            graph = "Font";
        }
        private void button5_Click(object sender, EventArgs e)
        {
            //读取设置的两臂长度
            L1 = Convert.ToInt16(textBox6.Text);
            L2 = Convert.ToInt16(textBox7.Text);

            //绘制图像为直线
            if (graph == "Line")
            {
                //将上一次的机械臂的末端位置（即现在机械臂末端位置）设置为起点位置
                startX = endX;
                startY = endY;
                //将绘制目标图像（即线段）的起点位置设置为终点位置
                endX = double.Parse(textBox1.Text);
                endY = double.Parse(textBox2.Text);
                //然后机械臂末端从起点沿直线运动到终点位置，并且这段移动是不需要绘制轨迹的，所以为false
                DrawLine(startX, startY, endX, endY, false);
                //将上一次的机械臂的末端位置（即上面说的目标图像的起点位置）设置为起点位置
                startX = endX;
                startY = endY;
                //将绘制目标图像的终点位置设置为终点位置
                endX = double.Parse(textBox3.Text);
                endY = double.Parse(textBox4.Text);
                //然后机械臂末端从起点沿直线运动到终点位置，这段移动是需要绘制轨迹的，所以为true
                DrawLine(startX, startY, endX, endY, true);
            }
            //绘制图像为圆
            if (graph == "Circle")
            {
                //将上一次的机械臂的末端位置（即现在机械臂末端位置）设置为起点位置
                startX = endX;
                startY = endY;
                //将绘制目标图像（即圆）的起点位置（即圆的最左点）设置为终点位置
                endX = double.Parse(textBox1.Text) - double.Parse(textBox3.Text);
                endY = double.Parse(textBox2.Text);
                //然后机械臂末端从起点沿直线运动到终点位置，并且这段移动是不需要绘制轨迹的，所以为false
                DrawLine(startX, startY, endX, endY, false);
                //最后开始画圆，这段移动是需要绘制轨迹的，所以为true
                DrawCircle(double.Parse(textBox1.Text), double.Parse(textBox2.Text), double.Parse(textBox3.Text));
            }
            //绘制图像为人像轮廓
            if (graph == "Graph")
            {//轮廓
                for (int i = 0; i < contours.Size; i++)
                {
                    for (int j = 0; j < contours[i].Size - 1; j++)
                    {
                        //将上一次的机械臂的末端位置（即现在机械臂末端位置）设置为起点位置
                        startX = endX;
                        startY = endY;
                        //将绘制目标图像（即轮廓的第i组的第j条线段）的起点位置（即圆的最左点）设置为终点位置
                        endX = contours[i][j].X - 40;
                        endY = -(contours[i][j].Y - 125);
                        //然后机械臂末端从起点沿直线运动到终点位置，并且这段移动是不需要绘制轨迹的，所以为false
                        DrawLine(startX, startY, endX, endY, false);
                        //将上一次的机械臂的末端位置（即上面说的轮廓的第i组的第j条线段的起点位置）设置为起点位置
                        startX = endX;
                        startY = endY;
                        //将机械臂的目标末端位置（即上面说的轮廓的第i组的第j条线段的终点位置）设置为起点位置
                        endX = contours[i][j + 1].X - 40;
                        endY = -(contours[i][j + 1].Y - 125);
                        //然后绘制轮廓的第i组第j条线段
                        DrawLine(startX, startY, endX, endY, true);
                        pointLast = new PointF(0, 0);//每一次图像绘制成功后，将pointLast设置（0，0）（与定时器配合使用），自行体会作用，感觉讲不清
                    }
                }
            }
            //绘制图像为汉字
            if (graph == "Font")
            {
                for (int j = 0; j < textBox5.Text.Length; j++)
                {
                    char word = textBox5.Text[j];//取出第j个文字
                    //将这个汉字转化为GB22313编码，并且加0构成文件名
                    byte[] wordbytes = Encoding.GetEncoding("GB2312").GetBytes(new char[] { word });
                    string filename = "0" + Convert.ToString((wordbytes[0] << 8) + wordbytes[1], 16);
                    //从文件中读出数据
                    StreamReader sr = new StreamReader("E:/下载软件/Visual Studio 2017/C#文件/Robotic Arm/汉字库/" + filename + ".txt");
                    string wordtxt = sr.ReadToEnd();//将文字所有信息读入wordtxt变量
                    string font = wordtxt.Split(new string[] { "7,-114,5,", "7,-113,0" }, StringSplitOptions.RemoveEmptyEntries)[1];//切取有用片段
                    string[] fontarray = font.Split(new string[] { ",", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);//将字符串转化为字符数组，去掉逗号空格这些
                    int i = 0;
                    //调整字的初始位置，即字的左下角坐标
                    Point point = new Point(100 * j - 50, 0);
                    //将上一次机械臂末端位置设置为起点位置
                    startX = endX;
                    startY = endY;
                    //将字的左下角位置设置为机械臂末端目标位置
                    endX = point.X;
                    endY = point.Y;
                    //使机械臂从当前位置沿直线移动到字的走下角位置，此过程不需要画轨迹，故为false
                    DrawLine(startX, startY, endX, endY, false);
                    //解码文字信息，结合Shape文件格式解码出想要的数据
                    while (i < fontarray.Length)
                    {
                        if (fontarray[i] == "2")//说明为抬笔过程，就不用画轨迹，故为false
                        {
                            startX = endX;
                            startY = endY;
                            endX = startX + Convert.ToInt16(fontarray[i + 2]);
                            endY = startY + Convert.ToInt16(fontarray[i + 3]);
                            DrawLine(startX, startY, endX, endY, false);
                            i = i + 4;
                        }
                        if (fontarray[i++] == "1")//说明为落笔过程，需要画轨迹，故为true
                        {
                            for (; i < fontarray.Length && fontarray[i] != "2";)
                            {
                                if (fontarray[i] == "8")
                                {
                                    startX = endX;
                                    startY = endY;
                                    endX = startX + Convert.ToInt16(fontarray[i + 1]);
                                    endY = startY + Convert.ToInt16(fontarray[i + 2]);
                                    DrawLine(startX, startY, endX, endY, true);
                                    i = i + 3;
                                }
                                else if (fontarray[i] == "12")//因为用到圆弧的字较少，所以忽略
                                {

                                }
                            }
                            pointLast = new PointF(0, 0);//每一笔绘制成功后，将pointLast设置（0，0）（与定时器配合使用），自行体会作用
                        }
                    }
                }
            }
            pointLast = new PointF(0, 0);

        }

        #region 绘制机械臂

        /// <summary>
        /// 机械臂的底端位于坐标系原点
        /// </summary>
        public void DrawMechanism()
        {

            //特别注意：我定义的坐标系为我们平常时常用的坐标系，
            //但电脑屏幕等运用的坐标系是：左上角为原点，水平向右为X正半轴，水平向下为Y正半轴
            //因此两个坐标系之间存在装换。 
            PointF pointO = new PointF(200F, 200F);//机械臂O点位置，位于坐标原点
            PointF pointA = new PointF((float)(200 + L1 * Math.Cos(Ang1)), (float)(200 - L1 * Math.Sin(Ang1)));//机械臂A点位置
            PointF pointB = new PointF((float)(200 + L1 * Math.Cos(Ang1) + L2 * Math.Cos(Ang1 + Ang2)), (float)(200 - L1 * Math.Sin(Ang1) - L2 * Math.Sin(Ang1 + Ang2)));//机械臂B点位置

            Bitmap bitmap = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);//位图为PictureBox1的客户区尺寸
            Graphics g = Graphics.FromImage(bitmap);//定义图像，图像为抽象类，不可直接构造函数生成
            //绘制斜线时消除锯齿（鼠标放在那就可以看到函数功能）
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SmoothingMode = SmoothingMode.HighQuality;
            Pen pen = new Pen(Color.Red, 1);//定义一只红色、1像素宽的笔
            g.DrawLine(pen, pointO, pointA);//绘制大臂（连接OA线段）
            g.DrawLine(pen, pointA, pointB);//绘制小臂（连接AB线段）
            //绘制转动副O
            g.DrawEllipse(pen, 200 - 5, 200 - 5, 10, 10);
            g.FillEllipse(new SolidBrush(Color.White), 200 - 5, 200 - 5, 10, 10);
            //绘制转动副A
            g.DrawEllipse(pen, (float)(200 + L1 * Math.Cos(Ang1)) - 5, (float)(200 - L1 * Math.Sin(Ang1)) - 5, 10, 10);
            g.FillEllipse(new SolidBrush(Color.White), (float)(200 + L1 * Math.Cos(Ang1)) - 5, (float)(200 - L1 * Math.Sin(Ang1)) - 5, 10, 10);//绘制转动副A

            pictureBox1.Image = bitmap;//设置为控件的图片
            //释放资源
            g.Dispose();
            pen.Dispose();
        }
        #endregion

        #region 求解对应坐标下两臂转动角度

        /// <summary>
        /// 求解两臂转动角度,（反）三角函数都是弧度单位。
        /// </summary>
        /// <param name="X">目标点的Y坐标（对应我定义的坐标系）</param>
        /// <param name="Y">目标点的Y坐标（对应我定义的坐标系）</param>
        public void GetAngel(double X, double Y)
        {
            double x = X;
            double y = Y;
            double r = Math.Sqrt(x * x + y * y);//目标点到原点的距离
            double theta = Math.Atan2(y, x);//取值范围为（-PI,PI）
            double phi = Math.PI - Math.Acos((L1 * L1 + L2 * L2 - r * r) / (2 * L1 * L2));
            double theta1 = Math.Acos((r * r + L1 * L1 - L2 * L2) / (2 * L1 * r));
            if (r == 0)
            {
                Ang2 = Math.PI;
            }
            //两个解:（theta+theta1，-phi）、（theta-theta1，phi）
            //选解采用最短准则：即对应的两臂角度相对于上一时刻的两臂角度需要变动的角度和更小的解
            else
            {
                if ((Math.Abs(theta + theta1 - Ang1) + Math.Abs(-phi - Ang2)) > (Math.Abs(theta - theta1 - Ang1) + Math.Abs(phi - Ang2)))
                {
                    Ang1 = theta - theta1;
                    Ang2 = phi;
                }
                else
                {
                    Ang1 = theta + theta1;
                    Ang2 = -phi;
                }
            }
            textBox8.Text = (Ang1 / Math.PI * 180).ToString();//将弧度换算成角度显示
            textBox9.Text = (Ang2 / Math.PI * 180).ToString();//
        }


        #endregion

        #region 绘制两点之间的一条直线

        /// <summary>
        /// 绘制两点之间的一条直线。
        /// </summary>
        /// <param name="startx">起点的X坐标</param>
        /// <param name="starty">起点的Y坐标</param>
        /// <param name="endx">终点的x坐标</param>
        /// <param name="X">终点的Y坐标</param>
        /// <param name="bl">是否是绘图，绘图:true;移动:false</param>
        public void DrawLine(double startx, double starty, double endx, double endy, Boolean bl)
        {
            if (bl)//如果是绘图，就开启Timer1进行绘制轨迹
            {
                timer1.Enabled = true;
            }
            double x, y;//用来表示下一个机械臂末端位置
            double r = Math.Sqrt((startx - endx) * (startx - endx) + (starty - endy) * (starty - endy));//直线长度
            for (int i = 0; i < r / 0.05; i++)//对所画图形进行分段计数
            {
                x = startx + i / (r / 0.05) * (endx - startx);
                y = starty + i / (r / 0.05) * (endy - starty);
                GetAngel(x, y);//通过调用此函数，计算得到对应（x，y）点时的两臂角度，对全局变量Ang1和Ang2赋值
                DrawMechanism();//通过Ang1和Ang2的实时数据，更新机械臂位置，因为每次变化很小，肉眼上以为机械臂在转动
                DelayMs(4);//每个机械臂状态停留4ms，如果不停留，程序运行很快，基本基本看不到机械臂中间移动过程
            }
            timer1.Enabled = false;//无论是机械臂绘图还是单纯移动到目标点，都关闭定时器
        }


        #endregion

        #region 绘制一个圆

        /// <summary>
        /// 绘制给定圆心和半径的圆。
        /// </summary>
        /// <param name="startx">圆心的X坐标</param>
        /// <param name="starty">圆心的Y坐标</param>
        /// <param name="r">圆的半径</param>
        public void DrawCircle(double startx, double starty, double r)
        {
            timer1.Enabled = true;
            double x, y;
            double theta;//存储画圆时的弧度，取值范围（-PI,PI）
            double k = Math.PI * r / 0.05; //将圆的周长细分为2k个点
            for (int i = 0; i < 2 * k; i++)//从圆的最左点开始逆时针绘制，即从-PI到PI绘制
            {
                theta = (i - k) / k * Math.PI;//每个点对应的弧度
                //计算点的坐标
                x = startx + r * Math.Cos(theta);
                y = starty + r * Math.Sin(theta);
                GetAngel(x, y);
                DrawMechanism();
                DelayMs(10);
            }
            timer1.Enabled = false;
        }
        #endregion

        #region 延时函数

        /// <summary>
        /// 延时函数，单位为毫秒
        /// </summary>
        /// <param name="delayTime"></param>
        public void DelayMs(int delayTime)
        {
            DateTime now = DateTime.Now;
            int s;
            do
            {
                TimeSpan spand = DateTime.Now - now;
                s = spand.Milliseconds;
                Application.DoEvents();
            }
            while (s < delayTime);
        }
        #endregion
    }
}
