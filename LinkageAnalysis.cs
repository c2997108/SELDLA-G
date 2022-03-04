using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using SkiaSharp;
using System.Text;

namespace SELDLA_G
{
    public class LinkageAnalysis : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        Texture2D whiteRectangle;
        Texture2D texture;
        Texture2D texturePop;
        SKBitmap bitmap;
        SKCanvas canvas;
        SKPaint paintPop;
        int maxItemsSize = 1000 * 1000;
        int worldX = 0;
        int worldY = 80;
        int inworldX = 0;
        int inworldY = 0;
        double worldW = 1;
        Point? lastPos;
        Point stage = new Point(0, 0);
        float[,] distphase3;
        int num_markers;
        List<PhaseData> myphaseData;
        string[] myheader;
        Dictionary<string, List<int>> myfamily = new Dictionary<string, List<int>>();
        MarkerPos pos1 = new MarkerPos();
        MarkerPos pos2 = new MarkerPos();
        bool changing = false;
        int markN = 0;
        int[] markNstart = new int[3];
        int[] markNend = new int[3];
        int markM = 0;
        int[] markMstart1 = new int[3];
        int[] markMend1 = new int[3];
        int[] markMstart2 = new int[3];
        int[] markMend2 = new int[3];
        int markF = 0;
        int[] markFstart = new int[3];
        int[] markFend = new int[3];
        int markG = 0;
        int[] markGstart1 = new int[3];
        int[] markGend1 = new int[3];
        int[] markGstart2 = new int[3];
        int[] markGend2 = new int[3];
        string savefileprefixname = "seldlag_output";
        int n_connect_length = 10000;
        Dictionary<string, string> seq = new Dictionary<string, string>();
        List<ContigPos> contigPositions;
        Dictionary<string, int> chrbpsize = new Dictionary<string, int>();
        Dictionary<string, float> chrcmsize = new Dictionary<string, float>();
        string fileSeq = "demo_shiitake_sequence.txt";
        //string fileSeq = "../../../cl0.92_sp0.90_ex0.60_split_seq.txt";
        string filePhase = "demo_shiitake_phase.txt";
        //string filePhase = "../../../savedate.txt";
        //string filePhase = "../../../seldla2nd_chain.ld2imp.all.txt";
        //string filePhase = "../../../seldla2nd_chain.ph.all.txt";
        float[,] backdistphase3;
        List<PhaseData> backmyphaseData = new List<PhaseData>();
        float[,] saveddistphase3;
        List<PhaseData> savedmyphaseData = new List<PhaseData>();
        int colorvari = 2; //1: black, 2:white


        public LinkageAnalysis()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

        }

        void openFile(string filename)
        {
            myfamily = new Dictionary<string, List<int>>();
            Regex reg = new Regex("^[^#]");

            myheader = File.ReadLines(filename).Take(1).Select(line =>
            {
                var items = line.Split("\t");
                return items;
            }).ToArray()[0];
            for(int i=6; i<myheader.Length; i++)
            {
                var items = myheader[i].Split("#");
                if(items.Length == 2)
                {
                    if (!myfamily.ContainsKey(items[0]))
                    {
                        List <int> tempfamily = new List <int>();
                        myfamily.Add(items[0], tempfamily);
                    }
                    myfamily[items[0]].Add(i-6);
                }
                else
                {
                    if (!myfamily.ContainsKey("#"))
                    {
                        List<int> tempfamily = new List<int>();
                        myfamily.Add("#", tempfamily);
                    }
                    myfamily["#"].Add(i - 6);
                }
            }

            myphaseData = File.ReadLines(filename)
                                        .Where(c => reg.IsMatch(c))
                                        //.Take(30000)
                                        .AsParallel().AsOrdered()
                                        .Select(line => {
                                            var items = line.Split("\t");
                                            PhaseData phase = new PhaseData();
                                            List<int> phasedata = new List<int>();
                                            phase.chr2nd = items[0];
                                            phase.chrorig = items[4];
                                            phase.markerpos = items[5];
                                            if ((items[2] == "+" && items[3] == "+") || (items[2] == "-" && items[3] == "-"))
                                            {
                                                phase.chrorient = "+";
                                            }
                                            else if ((items[2] == "+" && items[3] == "-") || (items[2] == "-" && items[3] == "+"))
                                            {
                                                phase.chrorient = "-";
                                            }
                                            else
                                            {
                                                phase.chrorient = "na";
                                            }
                                            for (int i = 6; i < items.Length; i++)
                                            {
                                                if (items[i] == "1")
                                                {
                                                    phasedata.Add(1);
                                                }
                                                else if (items[i] == "0")
                                                {
                                                    phasedata.Add(-1);
                                                }
                                                else
                                                {
                                                    phasedata.Add(0);
                                                }
                                            }
                                            phase.dataphase = phasedata;
                                            return phase;
                                        }).ToList();

            num_markers = myphaseData.Count;
            updatePhaseIndex();

        }
        void updatePhaseIndex()
        {
            for (int i = 0; i < myphaseData.Count; i++)
            {
                myphaseData[i].chrorigStartIndex = -1;
            }
            for (int i = 0; i < myphaseData.Count; i++)
            {
                PhaseData tempphase = myphaseData[i];
                if (tempphase.chrorigStartIndex == -1)
                {
                    tempphase.chrorigStartIndex = i;
                    for (int j = i; j < myphaseData.Count; j++)
                    {
                        if (tempphase.chrorig == myphaseData[j].chrorig)
                        {
                            tempphase.chrorigEndIndex = j;
                        }
                        else
                        {
                            break;
                        }
                    }
                    for (int j = i + 1; j <= tempphase.chrorigEndIndex; j++)
                    {
                        myphaseData[j].chrorigStartIndex = i;
                        myphaseData[j].chrorigEndIndex = tempphase.chrorigEndIndex;
                    }
                }
            }
        }
        /*static void CalcMatchRateKernel(Index2D index, int n_markers, int n_samples, ArrayView<int> data, ArrayView<float> output)
        {
            int sum1 = 0;
            int sum2 = 0;
            int n = 0;
            int i = index.X;
            int j = index.Y;
            for (int k = 0; k < n_samples; k++)
            {
                if (data[i * n_samples + k] != 0 && data[j * n_samples + k] != 0)
                {
                    n++;
                    if (data[i * n_samples + k] == data[j * n_samples + k])
                    {
                        sum1++;
                    }
                    if (data[i * n_samples + k] == -data[j * n_samples + k])
                    {
                        sum2++;
                    }
                }
            }
            if (n == 0) { 
                output[i * n_markers + j] = 0;
            }
            else
            {
                if (sum1 > sum2)
                {
                    output[i * n_markers + j] = 2 * sum1/(float)n - 1.0f;
                }
                else
                {
                    output[i * n_markers + j] = 2 * sum2/(float)n - 1.0f;
                }
            }
        }
        static void CalcMatchRate1lineKernel(Index1D index, int j, int n_markers, int n_samples, ArrayView<int> data, ArrayView<float> output)
        {
            int sum1 = 0;
            int sum2 = 0;
            int n = 0;
            int i = index.X;
            for (int k = 0; k < n_samples; k++)
            {
                if (data[i * n_samples + k] != 0 && data[j * n_samples + k] != 0)
                {
                    n++;
                    if (data[i * n_samples + k] == data[j * n_samples + k])
                    {
                        sum1++;
                    }
                    if (data[i * n_samples + k] == -data[j * n_samples + k])
                    {
                        sum2++;
                    }
                }
            }
            if (n == 0)
            {
                output[i] = 0;
            }
            else
            {
                if (sum1 > sum2)
                {
                    output[i] = 2 * sum1 / (float)n - 1.0f;
                }
                else
                {
                    output[i] = 2 * sum2 / (float)n - 1.0f;
                }
            }
        }*/
        static void CalcMatchN1lineKernel(Index1D index, int startIndex, int endIndex, int n_markers, int n_samples, ArrayView<int> data, ArrayView<float> output, ArrayView<float> outputN)
        {
            int i = index.X;
            for (int j = startIndex; j <= endIndex; j++)
            {
                int sum1 = 0;
                int sum2 = 0;
                int n = 0;
                for (int k = 0; k < n_samples; k++)
                {
                    if (data[i * n_samples + k] != 0 && data[j * n_samples + k] != 0)
                    {
                        n++;
                        if (data[i * n_samples + k] == data[j * n_samples + k])
                        {
                            sum1++;
                        }
                        if (data[i * n_samples + k] == -data[j * n_samples + k])
                        {
                            sum2++;
                        }
                    }
                }
                outputN[i + (j - startIndex) * n_markers] = n;
                if (n == 0)
                {
                    output[i + (j - startIndex) * n_markers] = 0;
                }
                else
                {
                    if (sum1 > sum2)
                    {
                        output[i + (j - startIndex) * n_markers] = sum1;
                    }
                    else
                    {
                        output[i + (j - startIndex) * n_markers] = sum2;
                    }
                }
            }
        }
        void calcMatchRate1line()
        {
            int num_rows = 0;
            for (int i = 0; i < num_markers; i++)
            {
                num_rows++;
                if (num_markers * (i + 1) > maxItemsSize) break;
            }
            distphase3 = new float[num_markers, num_markers];
            int[] phaseForGPU;
            using Context context2 = Context.Create(builder => builder.AllAccelerators());
            //using Context context2 = Context.Create(builder => builder.OpenCL());
            Accelerator accelerator2 = context2.GetPreferredDevice(preferCPU: false).CreateAccelerator(context2);
            //Accelerator accelerator2 = context2.GetPreferredDevice(preferCPU: true).CreateAccelerator(context2);
            accelerator2.PrintInformation();

            if(accelerator2.AcceleratorType == AcceleratorType.CPU) //CPUの場合
            {
                float[,] tempdataN = new float[num_markers, num_markers];
                float[,] tempdataV = new float[num_markers, num_markers];
                System.Threading.Tasks.Parallel.For(0, num_markers, j => {
                    for (int i = 0; i < num_markers; i++)
                    {
                        foreach (var eachfamily in myfamily.Values)
                        {
                            int sum1 = 0;
                            int sum2 = 0;
                            int n = 0;
                            int n_samples = eachfamily.Count;
                            foreach (var person in eachfamily)
                            {
                                if (myphaseData[i].dataphase[person] != 0 && myphaseData[j].dataphase[person] != 0)
                                {
                                    n++;
                                    if (myphaseData[i].dataphase[person] == myphaseData[j].dataphase[person])
                                    {
                                        sum1++;
                                    }
                                    if (myphaseData[i].dataphase[person] == -myphaseData[j].dataphase[person])
                                    {
                                        sum2++;
                                    }
                                }
                            }
                            tempdataN[i, j] = n;
                            if (n == 0)
                            {
                                tempdataV[i, j] += 0;
                            }
                            else
                            {
                                if (sum1 > sum2)
                                {
                                    tempdataV[i, j] += sum1;
                                }
                                else
                                {
                                    tempdataV[i, j] += sum2;
                                }
                            }
                        }
                    }
                });
                System.Threading.Tasks.Parallel.For(0, num_markers, j =>
                {
                    for (int i = 0; i < num_markers; i++)
                    {
                        distphase3[i, j] = 2 * tempdataV[i, j] / (float)tempdataN[i, j] - 1.0f;
                    }
                });
            }
            else //GPUが使えるときはこっち
            {
                float[,] distphaseN = new float[num_markers, num_markers]; //配列は0で初期化されている。言語仕様的に。
                float[,] distphaseV = new float[num_markers, num_markers];
                MemoryBuffer1D<int, Stride1D.Dense> deviceData2;
                MemoryBuffer1D<float, Stride1D.Dense> deviceOutputV = accelerator2.Allocate1D<float>(num_markers * num_rows);
                MemoryBuffer1D<float, Stride1D.Dense> deviceOutputN = accelerator2.Allocate1D<float>(num_markers * num_rows);
                Action<Index1D, int, int, int, int, ArrayView<int>, ArrayView<float>, ArrayView<float>> loadedKernel2 =
                    accelerator2.LoadAutoGroupedStreamKernel<Index1D, int, int, int, int, ArrayView<int>, ArrayView<float>, ArrayView<float>>(CalcMatchN1lineKernel);
                int n = -1;
                foreach (var eachfamily in myfamily.Values)
                {
                    n++;
                    //Console.WriteLine(n);
                    phaseForGPU = new int[num_markers * eachfamily.Count];
                    for (int i = 0; i < num_markers; i++)
                    {
                        int j = -1;
                        foreach (var person in eachfamily)
                        {
                            j++;
                            phaseForGPU[i * eachfamily.Count + j] = myphaseData[i].dataphase[person];
                        }
                    }
                    deviceData2 = accelerator2.Allocate1D(phaseForGPU);

                    for (int j = 0; j < num_markers; j++)
                    {
                        //Console.WriteLine(j);
                        int startIndex = j;
                        int endIndex = startIndex + num_rows - 1;
                        if (endIndex >= num_markers) endIndex = num_markers - 1;
                        loadedKernel2(new Index1D(num_markers), startIndex, endIndex, num_markers, eachfamily.Count, deviceData2.View, deviceOutputV.View, deviceOutputN.View);
                        accelerator2.Synchronize();
                        float[] hostOutputV = deviceOutputV.GetAsArray1D();
                        float[] hostOutputN = deviceOutputN.GetAsArray1D();
                        //Console.WriteLine(hostOutput2.Length);
                        for (int j2 = startIndex; j2 <= endIndex; j2++)
                        {
                            for (int i = 0; i < num_markers; i++)
                            {
                                    distphaseN[i, j2] += hostOutputN[i + (j2 - startIndex) * num_markers];
                                    distphaseV[i, j2] += hostOutputV[i + (j2 - startIndex) * num_markers];
                            }
                            //Console.WriteLine(j2);
                        }
                        j += endIndex - startIndex;
                    }
                }
                accelerator2.Dispose();
                context2.Dispose();

                System.Threading.Tasks.Parallel.For(0, num_markers, j =>
                {
                    for (int i = 0; i < num_markers; i++)
                    {
                        distphase3[i, j] = 2 * distphaseV[i, j] / (float)distphaseN[i, j] - 1.0f;
                    }
                });

            }
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            _graphics.PreferMultiSampling = false;
            _graphics.PreferredBackBufferWidth = GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsDevice.DisplayMode.Height;
            //_graphics.PreferredBackBufferWidth = 2000;
            //_graphics.PreferredBackBufferHeight = 1000;
            //_graphics.IsFullScreen = true;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // TODO: use this.Content to load your game content here
            Debug.WriteLine("LoadContent:");
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            whiteRectangle = new Texture2D(GraphicsDevice, 1, 1);
            whiteRectangle.SetData(new[] { Color.White });

            openFile(filePhase);

            texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
            calcMatchRate1line();
            setDistTexture();

            var w = GraphicsDevice.DisplayMode.Width; //500;
            var h = 80;
            bitmap = new SKBitmap(w, h);
            canvas = new SKCanvas(bitmap);
                // 透明色で塗りつぶす
                canvas.Clear(SKColors.Transparent);
            /*
                // 虹色グラデーションシェーダー
                var shader = SKShader.CreateSweepGradient(
                    new SKPoint(w / 2, h / 2),
                    new[] {
                        new SKColor(0x00,0x00,0xff),
                        new SKColor(0x00,0xdb,0xff),
                        new SKColor(0x00,0xff,0x49),
                        new SKColor(0x91,0xff,0x00),
                        new SKColor(0xff,0x93,0x00),
                        new SKColor(0xff,0x00,0x47),
                        new SKColor(0xdd,0x00,0xff),
                    }, null);

                var paint = new SKPaint
                {
                    Shader = shader,
                    StrokeWidth = 50,
                    IsStroke = true,
                };

                // 円を描く
                canvas.DrawCircle(w / 2, h / 2, w / 4, paint);
*/
            paintPop = new SKPaint();
            paintPop.TextSize = 25;
            paintPop.Color = SKColors.Black;
            //canvas.DrawText("test",10,50, paintPop);
            // 描画コマンド実行
            canvas.Flush();

            // SKBitmapをTexture2Dに変換
            texturePop = new Texture2D(GraphicsDevice, bitmap.Width, bitmap.Height, mipmap: false, format: SurfaceFormat.Color);
            texturePop.SetData(bitmap.Bytes);
            
        }
        void backupdata()
        {
            backdistphase3 = distphase3.Clone() as float[,];
            backmyphaseData = new List<PhaseData>();
            for(int i = 0; i < myphaseData.Count; i++)
            {
                backmyphaseData.Add(myphaseData[i].DeepCopy());
            }
        }
        void tempsavedata()
        {
            saveddistphase3 = distphase3.Clone() as float[,];
            savedmyphaseData = new List<PhaseData>();
            for (int i = 0; i < myphaseData.Count; i++)
            {
                savedmyphaseData.Add(myphaseData[i].DeepCopy());
            }
        }
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                markN = 0;
                markNstart = new int[3];
                markNend = new int[3];
                markM = 0;
                markMstart1 = new int[3];
                markMend1 = new int[3];
                markMstart2 = new int[3];
                markMend2 = new int[3];
                markF = 0;
                markFstart = new int[3];
                markFend = new int[3];
                markG = 0;
                markGstart1 = new int[3];
                markGend1 = new int[3];
                markGstart2 = new int[3];
                markGend2 = new int[3];
            }

            // TODO: Add your update logic here
            Debug.WriteLine("Update:");
            var mouse = Mouse.GetState();
            double oldworldW = worldW;
            worldW = Math.Pow(1.001, mouse.ScrollWheelValue);
            if (oldworldW != worldW)
            {
                worldX = (int)((worldX - mouse.X) * (worldW/oldworldW)+mouse.X);
                worldY = (int)((worldY - mouse.Y) * (worldW/oldworldW)+mouse.Y);
                Debug.WriteLine(", x: " + mouse.X + ", in world x: " + inworldX);
            }
            inworldX = (int)((mouse.X - worldX) / worldW);
            inworldY = (int)((mouse.Y - worldY) / worldW);
            int tempX = inworldX;
            if(tempX < 0) { tempX = 0; } else if(tempX >= num_markers){ tempX = num_markers - 1; }
            int tempY = inworldY;
            if (tempY < 0) { tempY = 0; } else if (tempY >= num_markers) { tempY = num_markers - 1; }
            if (tempX < tempY) { int temptemp = tempY; tempY = tempX; tempX = temptemp; }
            setPosData(tempX, pos2);
            setPosData(tempY, pos1);

            canvas.Clear(SKColors.White);
            canvas.DrawText("[Up]      Block number: "+pos1.X +", Chr: "+myphaseData[pos1.X].chr2nd+", Contig name: "+ myphaseData[pos1.X].chrorig+", Contig orientation: "+myphaseData[pos1.X].chrorient+", Marker position: "+myphaseData[pos1.X].markerpos, 10, 25, paintPop);
            canvas.DrawText("[Down] Block number: " + pos2.X + ", Chr: " + myphaseData[pos2.X].chr2nd + ", Contig name: " + myphaseData[pos2.X].chrorig + ", Contig orientation: " + myphaseData[pos2.X].chrorient + ", Marker position: " + myphaseData[pos2.X].markerpos, 10, 50, paintPop);
            canvas.DrawText("Phase identity: " +distphase3[pos1.X, pos2.X], 10, 75, paintPop);
            canvas.Flush();
            texturePop.SetData(bitmap.Bytes);

            Debug.WriteLine("wheel: " + mouse.ScrollWheelValue + ", bai" + worldW + ", worldX: " + worldX + ", x: " + mouse.X + ", in world x: " + inworldX);
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                if (lastPos == null)
                {
                    lastPos = new Point(mouse.X, mouse.Y);
                }
                Debug.WriteLine("x: "+mouse.X+", y: "+mouse.Y);
                worldX = stage.X + mouse.X-lastPos.Value.X;
                worldY = stage.Y + mouse.Y-lastPos.Value.Y;

            }
            else if(mouse.LeftButton == ButtonState.Released)
            {
                stage = new Point(worldX, worldY);
                lastPos = null;
            }
            // Poll for current keyboard state
            KeyboardState state = Keyboard.GetState();

            // Print to debug console currently pressed keys
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var key in state.GetPressedKeys())
                sb.Append("Key: ").Append(key).Append(" pressed ");

            if (sb.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine(sb.ToString());
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No Keys pressed");
                changing = false;
            }

            if (state.IsKeyDown(Keys.Z) && changing == false)
            {
                changing = true;

                if (backmyphaseData.Count > 0)
                {
                    myphaseData = backmyphaseData;
                    distphase3 = backdistphase3;
                    num_markers = myphaseData.Count;
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    backmyphaseData = new List<PhaseData>();
                    setDistTexture();
                }
            }
            if (state.IsKeyDown(Keys.C) && changing == false)
            {
                changing = true;

                tempsavedata();
            }
            if (state.IsKeyDown(Keys.V) && changing == false)
            {
                changing = true;

                if (savedmyphaseData.Count > 0)
                {
                    myphaseData = savedmyphaseData;
                    distphase3 = saveddistphase3;
                    num_markers = myphaseData.Count;
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    savedmyphaseData = new List<PhaseData>();
                    tempsavedata();
                    setDistTexture();
                }
            }
            // Move our sprite based on arrow keys being pressed:
            if (state.IsKeyDown(Keys.R) && changing == false)
            {
                changing = true;

                backupdata();
                updateDistanceReverse(pos1.chrStart, pos1.chrEnd);
                myphaseData = updatePhaseReverse(pos1.chrStart, pos1.chrEnd);
                updatePhaseIndex();
                //calcMatchRate1line();
                setDistTexture();
            }
            if (state.IsKeyDown(Keys.T) && changing == false)
            {
                changing = true;
                if(myphaseData[pos1.X].chr2nd != myphaseData[pos2.X].chr2nd)
                {
                    backupdata();
                    updateDistanceChange(pos1.chrStart, pos1.chrEnd, pos2.chrStart, pos2.chrEnd);
                    myphaseData = updatePhaseChange(pos1.chrStart, pos1.chrEnd, pos2.chrStart, pos2.chrEnd);
                    updatePhaseIndex();
                    //calcMatchRate1line();
                    setDistTexture();

                }
            }
            if (state.IsKeyDown(Keys.Y) && changing == false)
            {
                changing = true;

                backupdata();
                updateDistanceReverse(pos1.contigStart, pos1.contigEnd);
                myphaseData = updatePhaseReverse(pos1.contigStart, pos1.contigEnd);
                updatePhaseIndex();
                //calcMatchRate1line();
                setDistTexture();
            }
            if (state.IsKeyDown(Keys.U) && changing == false)
            {
                changing = true;
                if (myphaseData[pos1.X].chrorig != myphaseData[pos2.X].chrorig)
                {
                    backupdata();
                    updateDistanceChange(pos1.contigStart, pos1.contigEnd, pos2.contigStart, pos2.contigEnd);
                    myphaseData = updatePhaseChange(pos1.contigStart, pos1.contigEnd, pos2.contigStart, pos2.contigEnd);
                    updatePhaseIndex();
                    //calcMatchRate1line();
                    setDistTexture();

                }
            }
            if (state.IsKeyDown(Keys.F) && changing == false)
            {
                changing = true;
                if (markF == 0)
                {
                    markF = 1;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFend[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFstart[0] = i; } else { break; }
                    }
                }
                else if (markF == 1)
                {
                    markF = 0;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFend[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFstart[1] = i; } else { break; }
                    }
                    if (markFstart[0] < markFstart[1])
                    {
                        markFstart[2] = markFstart[0];
                        markFend[2] = markFend[1];
                    }
                    else
                    {
                        markFstart[2] = markFstart[1];
                        markFend[2] = markFend[0];
                    }

                    backupdata();
                    updateDistanceReverse(markFstart[2], markFend[2]);
                    myphaseData = updatePhaseReverse(markFstart[2], markFend[2]);
                    updatePhaseIndex();
                    //calcMatchRate1line();
                    setDistTexture();
                }
            }

            if (state.IsKeyDown(Keys.G) && changing == false)
            {
                changing = true;
                if (markG == 0)
                {
                    markG = 1;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGend1[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGstart1[0] = i; } else { break; }
                    }
                }
                else if (markG == 1)
                {
                    markG = 2;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGend1[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGstart1[1] = i; } else { break; }
                    }

                    if (markGstart1[0] < markGstart1[1])
                    {
                        markGstart1[2] = markGstart1[0];
                        markGend1[2] = markGend1[1];
                    }
                    else
                    {
                        markGstart1[2] = markGstart1[1];
                        markGend1[2] = markGend1[0];
                    }
                }
                else if (markG == 2)
                {
                    markG = 3;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGend2[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGstart2[0] = i; } else { break; }
                    }
                }
                else if (markG == 3)
                {
                    markG = 0;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGend2[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markGstart2[1] = i; } else { break; }
                    }

                    if (markGstart2[0] < markGstart2[1])
                    {
                        markGstart2[2] = markGstart2[0];
                        markGend2[2] = markGend2[1];
                    }
                    else
                    {
                        markGstart2[2] = markGstart2[1];
                        markGend2[2] = markGend2[0];
                    }

                    //1回目と2回目に選択した領域が入れ子になっていないことの確認
                    if (!(markGstart1[2] >= markGstart2[2] && markGstart1[2] <= markGend2[2])
                        && !(markGend1[2] >= markGstart2[2] && markGend1[2] <= markGend2[2])
                        && !(markGstart2[2] >= markGstart1[2] && markGstart2[2] <= markGend1[2])
                        && !(markGend2[2] >= markGstart1[2] && markGend2[2] <= markGend1[2]))
                    {
                        backupdata();
                        updateDistanceChange(markGstart1[2], markGend1[2], markGstart2[2], markGend2[2]);
                        myphaseData = updatePhaseChange(markGstart1[2], markGend1[2], markGstart2[2], markGend2[2]);
                        updatePhaseIndex();
                        //calcMatchRate1line();
                        setDistTexture();

                    }
                    else
                    {
                        Console.WriteLine("Error: Regions 1 and 2 overlap.");
                    }
                }
            }
            if (state.IsKeyDown(Keys.N) && changing == false)
            {
                changing = true;
                if (markN == 0)
                {
                    markN = 1;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markNend[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markNstart[0] = i; } else { break; }
                    }
                }
                else if(markN == 1)
                {
                    markN = 0;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig){markNend[1] = i;}else{break;}
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig){markNstart[1] = i;}else{break;}
                    }
                    if (markNstart[0] < markNstart[1])
                    {
                        markNstart[2] = markNstart[0];
                        markNend[2] = markNend[1];
                    }
                    else
                    {
                        markNstart[2] = markNstart[1];
                        markNend[2] = markNend[0];
                    }

                    backupdata();
                    updateDistanceReverse(markNstart[2], markNend[2]);
                    myphaseData = updatePhaseReverse(markNstart[2], markNend[2]);
                    updatePhaseIndex();
                    //calcMatchRate1line();
                    setDistTexture();
                }
            }

            if (state.IsKeyDown(Keys.M) && changing == false)
            {
                changing = true;
                if (markM == 0)
                {
                    markM = 1;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMend1[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMstart1[0] = i; } else { break; }
                    }
                }
                else if (markM == 1)
                {
                    markM = 2;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMend1[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMstart1[1] = i; } else { break; }
                    }

                    if (markMstart1[0] < markMstart1[1])
                    {
                        markMstart1[2] = markMstart1[0];
                        markMend1[2] = markMend1[1];
                    }
                    else
                    {
                        markMstart1[2] = markMstart1[1];
                        markMend1[2] = markMend1[0];
                    }
                }
                else if (markM == 2)
                {
                    markM = 3;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMend2[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMstart2[0] = i; } else { break; }
                    }
                }
                else if (markM == 3)
                {
                    markM = 0;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMend2[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markMstart2[1] = i; } else { break; }
                    }

                    if (markMstart2[0] < markMstart2[1])
                    {
                        markMstart2[2] = markMstart2[0];
                        markMend2[2] = markMend2[1];
                    }
                    else
                    {
                        markMstart2[2] = markMstart2[1];
                        markMend2[2] = markMend2[0];
                    }

                    //1回目と2回目に選択した領域が入れ子になっていないことの確認
                    if (!(markMstart1[2] >= markMstart2[2] && markMstart1[2] <= markMend2[2])
                        && !(markMend1[2] >= markMstart2[2] && markMend1[2] <= markMend2[2])
                        && !(markMstart2[2] >= markMstart1[2] && markMstart2[2] <= markMend1[2])
                        && !(markMend2[2] >= markMstart1[2] && markMend2[2] <= markMend1[2]))
                    {
                        backupdata();
                        updateDistanceChange(markMstart1[2], markMend1[2], markMstart2[2], markMend2[2]);
                        myphaseData = updatePhaseChange(markMstart1[2], markMend1[2], markMstart2[2], markMend2[2]);
                        updatePhaseIndex();
                        //calcMatchRate1line();
                        setDistTexture();

                    }
                    else
                    {
                        Console.WriteLine("Error: Regions 1 and 2 overlap.");
                    }
                }
            }

            if (state.IsKeyDown(Keys.D) && changing == false)
            {
                changing = true;

                backupdata();
                updateDistanceDelete(pos1.contigStart, pos1.contigEnd);
                myphaseData = updatePhaseDelete(pos1.contigStart, pos1.contigEnd);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.E) && changing == false)
            {
                changing = true;
                if (markN == 0)
                {
                    markN = 1;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markNend[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markNstart[0] = i; } else { break; }
                    }
                }
                else if (markN == 1)
                {
                    markN = 0;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markNend[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { markNstart[1] = i; } else { break; }
                    }
                    if (markNstart[0] < markNstart[1])
                    {
                        markNstart[2] = markNstart[0];
                        markNend[2] = markNend[1];
                    }
                    else
                    {
                        markNstart[2] = markNstart[1];
                        markNend[2] = markNend[0];
                    }

                    var templist = new List<string>();
                    for(int i = markNstart[2]; i <= markNend[2]; i++)
                    {
                        templist.Add(myphaseData[i].chr2nd);
                    }
                    Console.WriteLine("Original chr names:");
                    templist.Distinct().ToList().ForEach(c => Console.WriteLine(c));
                    Console.WriteLine("Input new chr name");
                    var str = Console.ReadLine();

                    backupdata();
                    List<PhaseData> tempmyphaseData = new List<PhaseData>();
                    for (int i = 0; i < num_markers; i++)
                    {
                        if (i >= markNstart[2] && i <= markNend[2])
                        {
                            myphaseData[i].chr2nd = str;
                        }

                    }
                }
            }

            if (state.IsKeyDown(Keys.W) && changing == false)
            {
                changing = true;
                if (markF == 0)
                {
                    markF = 1;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFend[0] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFstart[0] = i; } else { break; }
                    }
                }
                else if (markF == 1)
                {
                    markF = 0;
                    for (int i = pos1.X; i < num_markers; i++)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFend[1] = i; } else { break; }
                    }
                    for (int i = pos1.X; i >= 0; i--)
                    {
                        if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { markFstart[1] = i; } else { break; }
                    }
                    if (markFstart[0] < markFstart[1])
                    {
                        markFstart[2] = markFstart[0];
                        markFend[2] = markFend[1];
                    }
                    else
                    {
                        markFstart[2] = markFstart[1];
                        markFend[2] = markFend[0];
                    }

                    var templist = new List<string>();
                    for (int i = markFstart[2]; i <= markFend[2]; i++)
                    {
                        templist.Add(myphaseData[i].chr2nd);
                    }
                    Console.WriteLine("Original chr names:");
                    templist.Distinct().ToList().ForEach(c => Console.WriteLine(c));
                    Console.WriteLine("Input new chr name");
                    var str = Console.ReadLine();

                    backupdata();
                    List<PhaseData> tempmyphaseData = new List<PhaseData>();
                    for (int i = 0; i < num_markers; i++)
                    {
                        if (i >= markFstart[2] && i <= markFend[2])
                        {
                            myphaseData[i].chr2nd = str;
                        }

                    }
                }
            }

            if (state.IsKeyDown(Keys.S) && changing == false)
            {
                changing = true;

                savedata();
            }

            if (state.IsKeyDown(Keys.O) && changing == false)
            {
                changing = true;
                Console.WriteLine("Enter the name of the file you want to open. [\""+filePhase+ "\"]");
                var str = Console.ReadLine();
                if (str != "") { filePhase = str; }
                try
                {
                    backupdata();
                    openFile(filePhase);
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    calcMatchRate1line();
                    setDistTexture();

                }catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

            if (state.IsKeyDown(Keys.P) && changing == false)
            {
                changing = true;

                Console.WriteLine("Output the chromosome FASTA file as \"" + savefileprefixname + ".fasta\"");
                Console.WriteLine("Enter the contig FASTA file name. [\"" + fileSeq + "\"]");
                var str = Console.ReadLine();
                if (str != "") { fileSeq = str; }

                openseq(fileSeq);

            }
            if (state.IsKeyDown(Keys.H) && changing == false)
            {
                changing = true;

                if(colorvari == 1)
                {
                    colorvari = 2;
                }
                else
                {
                    colorvari = 1;
                }
                setDistTexture();
            }


            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);

            // TODO: Add your drawing code here
            Debug.WriteLine("Draw:");

            _spriteBatch.Begin();
            _spriteBatch.Draw(texture, new Vector2((float)worldX, (float)worldY), null, Color.White, 0.0f, Vector2.Zero, new Vector2((float)worldW, (float)worldW), SpriteEffects.None, 0.0f);

            if(colorvari == 1)
            {
                drawRect(_spriteBatch, whiteRectangle, pos1.chrStart, pos1.chrEnd - pos1.chrStart + 1, Color.Yellow);
                drawRect(_spriteBatch, whiteRectangle, pos2.chrStart, pos2.chrEnd - pos2.chrStart + 1, Color.Yellow);
            }
            else
            {
                drawRect(_spriteBatch, whiteRectangle, pos1.chrStart, pos1.chrEnd - pos1.chrStart + 1, Color.Blue);
                drawRect(_spriteBatch, whiteRectangle, pos2.chrStart, pos2.chrEnd - pos2.chrStart + 1, Color.Blue);
            }
            drawRect(_spriteBatch, whiteRectangle, pos1.contigStart, pos1.contigEnd - pos1.contigStart + 1, Color.LightGreen);
            drawRect(_spriteBatch, whiteRectangle, pos2.contigStart, pos2.contigEnd - pos2.contigStart + 1, Color.LightGreen);

            if (markF == 1)
            {
                int tempNstart = -1;
                int tempNend = -1;
                for (int i = pos1.X; i < num_markers; i++)
                {
                    if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { tempNend = i; } else { break; }
                }
                for (int i = pos1.X; i >= 0; i--)
                {
                    if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { tempNstart = i; } else { break; }
                }
                if (tempNstart >= markFstart[0])
                {
                    drawRect(_spriteBatch, whiteRectangle, markFstart[0], tempNend - markFstart[0] + 1, Color.Orange);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempNstart, markFend[0] - tempNstart + 1, Color.Orange);
                }

            }
            if (markG == 1)
            {
                int tempMstart1 = -1;
                int tempMend1 = -1;
                for (int i = pos1.X; i < num_markers; i++)
                {
                    if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { tempMend1 = i; } else { break; }
                }
                for (int i = pos1.X; i >= 0; i--)
                {
                    if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { tempMstart1 = i; } else { break; }
                }
                if (tempMstart1 >= markGstart1[0])
                {
                    drawRect(_spriteBatch, whiteRectangle, markGstart1[0], tempMend1 - markGstart1[0] + 1, Color.Orange);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempMstart1, markGend1[0] - tempMstart1 + 1, Color.Orange);
                }
            }
            else if (markG == 2)
            {
                drawRect(_spriteBatch, whiteRectangle, markGstart1[2], markGend1[2] - markGstart1[2] + 1, Color.Orange);
            }
            else if (markG == 3)
            {
                drawRect(_spriteBatch, whiteRectangle, markGstart1[2], markGend1[2] - markGstart1[2] + 1, Color.Orange);
                int tempMstart1 = -1;
                int tempMend1 = -1;
                for (int i = pos1.X; i < num_markers; i++)
                {
                    if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { tempMend1 = i; } else { break; }
                }
                for (int i = pos1.X; i >= 0; i--)
                {
                    if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd) { tempMstart1 = i; } else { break; }
                }
                if (tempMstart1 >= markGstart2[0])
                {
                    drawRect(_spriteBatch, whiteRectangle, markGstart2[0], tempMend1 - markGstart2[0] + 1, Color.Orange);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempMstart1, markGend2[0] - tempMstart1 + 1, Color.Orange);
                }
            }
            if (markN == 1)
            {
                int tempNstart = -1;
                int tempNend = -1;
                for (int i = pos1.X; i < num_markers; i++)
                {
                    if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { tempNend = i; } else { break; }
                }
                for (int i = pos1.X; i >= 0; i--)
                {
                    if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { tempNstart = i; } else { break; }
                }
                if (tempNstart >= markNstart[0])
                {
                    drawRect(_spriteBatch, whiteRectangle, markNstart[0],tempNend - markNstart[0] + 1,Color.Orange);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempNstart,markNend[0] - tempNstart + 1,Color.Orange);
                }

            }
            if (markM == 1)
            {
                int tempMstart1 = -1;
                int tempMend1 = -1;
                for (int i = pos1.X; i < num_markers; i++)
                {
                    if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { tempMend1 = i; } else { break; }
                }
                for (int i = pos1.X; i >= 0; i--)
                {
                    if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { tempMstart1 = i; } else { break; }
                }
                if (tempMstart1 >= markMstart1[0])
                {
                    drawRect(_spriteBatch, whiteRectangle, markMstart1[0], tempMend1 - markMstart1[0] + 1, Color.Orange);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempMstart1, markMend1[0] - tempMstart1 + 1, Color.Orange);
                }
            }else if(markM == 2)
            {
                drawRect(_spriteBatch, whiteRectangle, markMstart1[2], markMend1[2] - markMstart1[2] + 1, Color.Orange);
            }else if(markM == 3)
            {
                drawRect(_spriteBatch, whiteRectangle, markMstart1[2], markMend1[2] - markMstart1[2] + 1, Color.Orange);
                int tempMstart1 = -1;
                int tempMend1 = -1;
                for (int i = pos1.X; i < num_markers; i++)
                {
                    if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { tempMend1 = i; } else { break; }
                }
                for (int i = pos1.X; i >= 0; i--)
                {
                    if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig) { tempMstart1 = i; } else { break; }
                }
                if (tempMstart1 >= markMstart2[0])
                {
                    drawRect(_spriteBatch, whiteRectangle, markMstart2[0], tempMend1 - markMstart2[0] + 1, Color.Orange);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempMstart1, markMend2[0] - tempMstart1 + 1, Color.Orange);
                }
            }

            _spriteBatch.Draw(texturePop, new Vector2(0, 0), Color.White);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        void openseq(string file)
        {
            contigPositions = new List<ContigPos>();
            seq = File.ReadLines(file).AsParallel().ToDictionary(x => x.Split("\t")[0], x=>x.Split("\t")[1]);
/*            seq = new Dictionary<string, string>();
            File.ReadLines(file).AsParallel().AsOrdered().ForAll(line =>
            {
                var arr = line.Split("\t");
                seq.Add(arr[0], arr[1]);
            });*/
            Console.WriteLine(seq.Count);
            
            var extendedSeq = new Dictionary<string, StringBuilder>();
            var extendedSeqNAexcludedChr = new Dictionary<string, StringBuilder>();
            var strN = getNstr(n_connect_length);
            string oldcontigname = "";
            int notNAEndindex = -1;
            List<string> flagIsUsedContig = new List<string>();
            int length_orient = 0;
            int length_locate = 0;
            int length_all = 0;
            int n_orient = 0;
            int n_locate = 0;
            int n_all = 0;
            foreach (var phase in myphaseData)
            {
                if (phase.chrorig != oldcontigname)
                {
                    //集計用情報取得
                    flagIsUsedContig.Add(phase.chrorig);
                    n_all++;
                    length_all += seq[phase.chrorig].Length;
                    n_locate++;
                    length_locate += seq[phase.chrorig].Length;
                    if (phase.chrorient != "na")
                    {
                        n_orient++;
                        length_orient += seq[phase.chrorig].Length;
                    }

                    //chainファイル用データ作成
                    ContigPos tempcpos = new ContigPos { chrname = phase.chr2nd, contigname = phase.chrorig, orientation = phase.chrorient};
                    ContigPos oldcpos = null;
                    if(contigPositions.Count>0) oldcpos = contigPositions[contigPositions.Count - 1];
                    if(oldcpos != null && oldcpos.chrname == tempcpos.chrname)
                    {
                        tempcpos.start_bp = oldcpos.end_bp + n_connect_length + 1;
                        if(phase.chrorient == "na")
                        {
                            tempcpos.start_cm = oldcpos.end_cm;
                        }
                        else if(notNAEndindex != -1)
                        {
                            tempcpos.start_cm = oldcpos.end_cm + 100 * (1 - (distphase3[notNAEndindex, phase.chrorigStartIndex] + 1) / 2);
                        }
                        else //notNAEndindex == -1
                        {
                            tempcpos.start_cm = oldcpos.end_cm;
                        }
                    }
                    else
                    {
                        tempcpos.start_bp = 0;
                        tempcpos.start_cm = 0;
                    }
                    tempcpos.end_bp = tempcpos.start_bp + seq[phase.chrorig].Length - 1;
                    if(phase.chrorient == "na")
                    {
                        tempcpos.end_cm = tempcpos.start_cm;
                    }
                    else
                    {
                        tempcpos.end_cm = tempcpos.start_cm + 100 * (1 - (distphase3[phase.chrorigStartIndex, phase.chrorigEndIndex] + 1) / 2);
                        notNAEndindex = phase.chrorigEndIndex;
                    }
                    contigPositions.Add(tempcpos);
                    if (chrbpsize.ContainsKey(tempcpos.chrname))
                    {
                        chrbpsize[tempcpos.chrname] = tempcpos.end_bp;
                        chrcmsize[tempcpos.chrname] = tempcpos.end_cm;
                    }
                    else
                    {
                        chrbpsize.Add(tempcpos.chrname, tempcpos.end_bp);
                        chrcmsize.Add(tempcpos.chrname, tempcpos.end_cm);
                    }

                    //FASTAファイル作成
                    oldcontigname = phase.chrorig;
                    if (!extendedSeq.ContainsKey(phase.chr2nd))
                    {
                        var tempsb = new StringBuilder();
                        tempsb.Append(seq[phase.chrorig]);
                        if(phase.chrorient == "+")
                        {
                            extendedSeq.Add(phase.chr2nd, tempsb);
                            extendedSeqNAexcludedChr.Add(phase.chr2nd, tempsb);
                        }
                        else if(phase.chrorient == "-")
                        {
                            string revseq = getRevComp(tempsb.ToString());
                            extendedSeq.Add(phase.chr2nd, new StringBuilder(revseq));
                            extendedSeqNAexcludedChr.Add(phase.chr2nd, new StringBuilder(revseq));
                        }
                        else //"na"
                        {
                            extendedSeq.Add(phase.chr2nd, tempsb);
                            extendedSeqNAexcludedChr.Add(phase.chr2nd, getNstr(tempsb.Length));
                            extendedSeqNAexcludedChr.Add(phase.chr2nd+"_related_"+phase.chrorig, tempsb);
                        }
                    }
                    else
                    {
                        extendedSeq[phase.chr2nd].Append(strN);
                        extendedSeqNAexcludedChr[phase.chr2nd].Append(strN);
                        if (phase.chrorient == "+")
                        {
                            extendedSeq[phase.chr2nd].Append(seq[phase.chrorig]);
                            extendedSeqNAexcludedChr[phase.chr2nd].Append(seq[phase.chrorig]);
                        }
                        else if(phase.chrorient == "-")
                        {
                            string revseq = getRevComp(seq[phase.chrorig]);
                            extendedSeq[phase.chr2nd].Append(revseq);
                            extendedSeqNAexcludedChr[phase.chr2nd].Append(revseq);
                        }
                        else //"na"
                        {
                            extendedSeq[phase.chr2nd].Append(seq[phase.chrorig]);
                            extendedSeqNAexcludedChr[phase.chr2nd].Append(getNstr(seq[phase.chrorig].Length));
                            extendedSeqNAexcludedChr.Add(phase.chr2nd + "_related_" + phase.chrorig, new StringBuilder(seq[phase.chrorig]));
                        }
                    }
                }

            }
            using (var fs = new System.IO.StreamWriter(savefileprefixname + ".stats", false))
            {
                seq.Keys.Where(x => !flagIsUsedContig.Contains(x)).ToList().ForEach(x =>
                {
                    n_all++;
                    length_all+=seq[x].Length;
                });
                Console.WriteLine("Total: " + n_all + " contigs, " + length_all + " bases");
                Console.WriteLine("Located: " + n_locate + " contigs, " + length_locate + " bases (" + (length_locate / (double)length_all*100) + "%)");
                Console.WriteLine("Oriented: " + n_orient + " contigs, " + length_orient + " bases (" + (length_orient / (double)length_all*100) + "%)");
                fs.WriteLine("Total: " + n_all + " contigs, " + length_all + " bases");
                fs.WriteLine("Located: " + n_locate + " contigs, " + length_locate + " bases (" + (length_locate / (double)length_all*100) + "%)");
                fs.WriteLine("Oriented: " + n_orient + " contigs, " + length_orient + " bases (" + (length_orient / (double)length_all*100) + "%)");
            }
            using (var fs = new System.IO.StreamWriter(savefileprefixname+".includeNA.fasta", false))
            {
                foreach (var item in extendedSeq)
                {
                    fs.WriteLine(">"+item.Key);
                    fs.WriteLine(item.Value);
                }
                seq.Keys.Where(x => !flagIsUsedContig.Contains(x)).ToList().ForEach(x =>
                {
                    fs.WriteLine(">" + x);
                    fs.WriteLine(seq[x]);
                });
            }
            using (var fs = new System.IO.StreamWriter(savefileprefixname + ".fasta", false))
            {
                foreach (var item in extendedSeqNAexcludedChr)
                {
                    fs.WriteLine(">" + item.Key);
                    fs.WriteLine(item.Value);
                }
                seq.Keys.Where(x => !flagIsUsedContig.Contains(x)).ToList().ForEach(x =>
                {
                    fs.WriteLine(">" + x);
                    fs.WriteLine(seq[x]);
                });
            }
            using (var fs = new System.IO.StreamWriter(savefileprefixname + ".chain", false))
            {
                foreach (var item in contigPositions)
                {
                    fs.WriteLine(item.chrname + "\t" + item.contigname + "\t" + item.orientation + "\t" + item.start_bp + "\t" + item.end_bp + "\t" + item.start_cm + "\t" + item.end_cm);
                }
            }

            drawGraph();
        }

        void drawGraph()
        {
            try
            {
                Console.WriteLine("Drawing genetical and phisycal map...");
                int per_width = 250;
                int per_height = 1000;
                int all_per_num = 8;
                List<string> chrs = chrbpsize.Keys.ToList();
                chrs.Sort((a, b) => chrbpsize[b].CompareTo(chrbpsize[a]));
                int num_big_ls = chrs.Count();
                int maxbp = chrbpsize.Max(x=>x.Value);
                float maxcm = chrcmsize.Max(x=>x.Value);

                var imageall = new SKBitmap(per_width * all_per_num, per_height * ((num_big_ls - 1) / all_per_num + 1));
                SKCanvas canvas = new SKCanvas(imageall);
                canvas.Clear();
                
                for (int i = 1; i <= num_big_ls; i++)
                {
                    int j = (i - 1) % all_per_num + 1;
                    int k = (i - 1) / all_per_num + 1;
                    drawChrBase(canvas, maxbp, maxcm, 1, i, chrbpsize[chrs[i-1]], chrcmsize[chrs[i-1]], (j - 1) * per_width, (k - 1) * per_height);
                    contigPositions.Where(x => x.chrname == chrs[i - 1]).ToList().ForEach(x => drawBpCm(canvas, 1, x, maxbp, maxcm, (j - 1) * per_width, (k - 1) * per_height));
                    drawChrFin(canvas, maxbp, maxcm, 1, i, chrbpsize[chrs[i - 1]], chrcmsize[chrs[i - 1]], (j - 1) * per_width, (k - 1) * per_height, chrs[i-1]);
                }
                var image = SKImage.FromBitmap(imageall);

                using (var stream = File.Create(savefileprefixname+".png"))
                {
                    var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    data.SaveTo(stream);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void drawChrBase(SKCanvas canvas, long maxbp, double maxcm, int fold, int num_ls, long each_maxbp, double each_maxcm, int startx, int starty)
        {
            var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.White
            };
            canvas.DrawRect(startx, starty, 250 * fold, 1000 * fold, paint);
            var pen = new SKPaint
            {
                Color = SKColors.Gray
            };
            canvas.DrawLine(startx + 175 * fold, starty + 50 * fold, startx + 175 * fold, starty + (50 + 900 * (float)(each_maxcm / maxcm)) * fold, pen);
        }

        void drawBpCm(SKCanvas canvas, int fold, ContigPos contig, long maxbp, double maxcm, int startx, int starty)
        {
            var pen = new SKPaint
            {
                Color = SKColors.Gray
            };
            var brush = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.LightGray
            };
            canvas.DrawLine(startx + 75 * fold, starty + (50 + 900 * ((float)contig.start_bp / maxbp)) * fold, startx + 175 * fold, starty + (50 + 900 * (float)(contig.start_cm / maxcm)) * fold, pen);
            canvas.DrawLine(startx + 75 * fold, starty + (50 + 900 * ((float)contig.end_bp / maxbp)) * fold, startx + 175 * fold, starty + (50 + 900 * (float)(contig.end_cm / maxcm)) * fold, pen);
            if (contig.orientation == "na")
            {
                pen.Color= SKColors.Blue;
                brush.Color= SKColors.White;
            }
            else
            {
                pen.Color = SKColors.Red;
                brush.Color = SKColors.LightGray;
            }
            canvas.DrawRect(startx + 26 * fold, starty + (50 + 900 * ((float)contig.start_bp / maxbp)) * fold, 48 * fold, 900 * (contig.end_bp - contig.start_bp) / (float)maxbp * fold, brush);
            canvas.DrawLine(startx + 175 * fold, starty + (50 + 900 * (float)(contig.start_cm / maxcm)) * fold, startx + 175 * fold, starty + (50 + 900 * (float)(contig.end_cm / maxcm)) * fold, pen);
            pen.Color = SKColors.Black;
            canvas.DrawLine(startx + 165 * fold, starty + (50 + 900 * (float)(contig.start_cm / maxcm)) * fold, startx + 185 * fold, starty + (50 + 900 * (float)(contig.start_cm / maxcm)) * fold, pen);
            canvas.DrawLine(startx + 165 * fold, starty + (50 + 900 * (float)(contig.end_cm / maxcm)) * fold, startx + 185 * fold, starty + (50 + 900 * (float)(contig.end_cm / maxcm)) * fold, pen);
        }
        public void drawChrFin(SKCanvas canvas, long maxbp, double maxcm, int fold, int num_ls, long each_maxbp, double each_maxcm, int startx, int starty, string chrname)
        {
            float y0 = 50;
            float y1 = 50 + 900 * each_maxbp / maxbp;
            var brush = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.White
            };
            var pen = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Black
            };

            var path = new SKPath { FillType = SKPathFillType.EvenOdd };
            path.MoveTo(startx + 25 * fold, starty + (float)(y0 - 5) * fold);
            path.LineTo(startx + 25 * fold, starty + (float)(y0 + 7.5 - 0) * fold);
            path.LineTo(startx + 30 * fold, starty + (float)(y0 + 7.5 - 3) * fold);
            path.LineTo(startx + 35 * fold, starty + (float)(y0 + 7.5 - 5.5) * fold);
            path.LineTo(startx + 40 * fold, starty + (float)(y0 + 7.5 - 6.5) * fold);
            path.LineTo(startx + 45 * fold, starty + (float)(y0 + 7.5 - 7) * fold);
            path.LineTo(startx + 50 * fold, starty + (float)(y0 + 7.5 - 7.5) * fold);
            path.LineTo(startx + 55 * fold, starty + (float)(y0 + 7.5 - 7) * fold);
            path.LineTo(startx + 60 * fold, starty + (float)(y0 + 7.5 - 6.5) * fold);
            path.LineTo(startx + 65 * fold, starty + (float)(y0 + 7.5 - 5.5) * fold);
            path.LineTo(startx + 70 * fold, starty + (float)(y0 + 7.5 - 3) * fold);
            path.LineTo(startx + 75 * fold, starty + (float)(y0 + 7.5 - 0) * fold);
            path.LineTo(startx + 75 * fold, starty + (float)(y0 - 5) * fold);
            path.Close();
            canvas.DrawPath(path, brush);

            path = new SKPath { FillType = SKPathFillType.EvenOdd };
            path.MoveTo(startx + 75 * fold, starty + (float)(y1 + 5) * fold);
            path.LineTo(startx + 75 * fold, starty + (float)(y1 - 7.5 + 0) * fold);
            path.LineTo(startx + 70 * fold, starty + (float)(y1 - 7.5 + 3) * fold);
            path.LineTo(startx + 65 * fold, starty + (float)(y1 - 7.5 + 5.5) * fold);
            path.LineTo(startx + 60 * fold, starty + (float)(y1 - 7.5 + 6.5) * fold);
            path.LineTo(startx + 55 * fold, starty + (float)(y1 - 7.5 + 7) * fold);
            path.LineTo(startx + 50 * fold, starty + (float)(y1 - 7.5 + 7.5) * fold);
            path.LineTo(startx + 45 * fold, starty + (float)(y1 - 7.5 + 7) * fold);
            path.LineTo(startx + 40 * fold, starty + (float)(y1 - 7.5 + 6.5) * fold);
            path.LineTo(startx + 35 * fold, starty + (float)(y1 - 7.5 + 5.5) * fold);
            path.LineTo(startx + 30 * fold, starty + (float)(y1 - 7.5 + 3) * fold);
            path.LineTo(startx + 25 * fold, starty + (float)(y1 - 7.5 + 0) * fold);
            path.LineTo(startx + 25 * fold, starty + (float)(y1 + 5) * fold);
            path.Close();
            canvas.DrawPath(path, brush);

            path = new SKPath { };
            path.MoveTo(startx + 25 * fold, starty + (float)(y0 + 7.5 - 0) * fold);
            path.LineTo(startx + 30 * fold, starty + (float)(y0 + 7.5 - 3) * fold);
            path.LineTo(startx + 35 * fold, starty + (float)(y0 + 7.5 - 5.5) * fold);
            path.LineTo(startx + 40 * fold, starty + (float)(y0 + 7.5 - 6.5) * fold);
            path.LineTo(startx + 45 * fold, starty + (float)(y0 + 7.5 - 7) * fold);
            path.LineTo(startx + 50 * fold, starty + (float)(y0 + 7.5 - 7.5) * fold);
            path.LineTo(startx + 55 * fold, starty + (float)(y0 + 7.5 - 7) * fold);
            path.LineTo(startx + 60 * fold, starty + (float)(y0 + 7.5 - 6.5) * fold);
            path.LineTo(startx + 65 * fold, starty + (float)(y0 + 7.5 - 5.5) * fold);
            path.LineTo(startx + 70 * fold, starty + (float)(y0 + 7.5 - 3) * fold);
            path.LineTo(startx + 75 * fold, starty + (float)(y0 + 7.5 - 0) * fold);

            path.LineTo(startx + 75 * fold, starty + (float)(y1 - 7.5 + 0) * fold);
            path.LineTo(startx + 70 * fold, starty + (float)(y1 - 7.5 + 3) * fold);
            path.LineTo(startx + 65 * fold, starty + (float)(y1 - 7.5 + 5.5) * fold);
            path.LineTo(startx + 60 * fold, starty + (float)(y1 - 7.5 + 6.5) * fold);
            path.LineTo(startx + 55 * fold, starty + (float)(y1 - 7.5 + 7) * fold);
            path.LineTo(startx + 50 * fold, starty + (float)(y1 - 7.5 + 7.5) * fold);
            path.LineTo(startx + 45 * fold, starty + (float)(y1 - 7.5 + 7) * fold);
            path.LineTo(startx + 40 * fold, starty + (float)(y1 - 7.5 + 6.5) * fold);
            path.LineTo(startx + 35 * fold, starty + (float)(y1 - 7.5 + 5.5) * fold);
            path.LineTo(startx + 30 * fold, starty + (float)(y1 - 7.5 + 3) * fold);
            path.LineTo(startx + 25 * fold, starty + (float)(y1 - 7.5 + 0) * fold);

            path.LineTo(startx + 25 * fold, starty + (float)(y0 + 7.5 - 0) * fold);
            canvas.DrawPath(path, pen);

            var font = new SKPaint { TextSize = 15 * fold, Color = SKColors.Black };
            canvas.DrawText(chrname, startx + 50 * fold, starty + 15 * fold, font);
            canvas.DrawText("0 bp", startx + 5 * fold, starty + 35 * fold, font);
            canvas.DrawText("0 cM", startx + 155 * fold, starty + 35 * fold, font);
            canvas.DrawText(each_maxbp.ToString("N0") + " bp", startx + 5 * fold, starty + (70 + 900 * (float)each_maxbp / maxbp) * fold, font);
            canvas.DrawText(each_maxcm.ToString("F1") + " cM", startx + 155 * fold, starty + (70 + 900 * (float)(each_maxcm / maxcm)) * fold, font);
            
        }

        void savedata()
        {

            Console.WriteLine("Enter the name of the file you want to save. [\"" + savefileprefixname + "\"]");
            var str = Console.ReadLine();
            if (str != "") { savefileprefixname = str; }

            string[] result = new string[num_markers+1];
            StringBuilder stra = new StringBuilder(myheader[0]);
            for(int i = 1; i < myheader.Length; i++)
            {
                stra.Append("\t"+myheader[i]);
            }
            result[0]=stra.ToString();

            for (int i = 0; i < result.Length-1; i++)
            {
                StringBuilder strb = new StringBuilder(myphaseData[i].chr2nd + "\t" + myphaseData[i].chr2nd);
                if (myphaseData[i].chrorient == "+" || myphaseData[i].chrorient == "-")
                {
                    strb.Append("\t+\t" + myphaseData[i].chrorient);
                }
                else
                {
                    strb.Append("\tna\t" + myphaseData[i].chrorient);
                }
                strb.Append("\t" + myphaseData[i].chrorig + "\t" + myphaseData[i].markerpos);
                for (int j = 0; j < myphaseData[i].dataphase.Count; j++)
                {
                    if (myphaseData[i].dataphase[j] == 1)
                    {
                        strb.Append("\t1");
                    }
                    else if (myphaseData[i].dataphase[j] == -1)
                    {
                        strb.Append("\t0");
                    }
                    else
                    {
                        strb.Append("\t-1");
                    }
                }
                strb.Append("\n");
                result[i+1] = strb.ToString();
            }
            Console.WriteLine("Saving to " + savefileprefixname+ ".phase.txt");
            System.IO.File.WriteAllLines(savefileprefixname+".phase.txt", result);

            try
            {
                texture.SaveAsPng(File.Create(savefileprefixname + ".contactmap.png"), num_markers, num_markers);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        void drawRect(SpriteBatch sprite, Texture2D rect, int inworldx, int size, Color color)
        {
            sprite.Draw(rect, new Rectangle((int)(inworldx * worldW + worldX), (int)(inworldx * worldW + worldY), (int)1, (int)(size * worldW)), color);
            sprite.Draw(rect, new Rectangle((int)((inworldx+size) * worldW + worldX), (int)(inworldx * worldW + worldY), (int)1, (int)(size * worldW)), color);
            sprite.Draw(rect, new Rectangle((int)(inworldx * worldW + worldX), (int)(inworldx * worldW + worldY), (int)(size*worldW), (int)1), color);
            sprite.Draw(rect, new Rectangle((int)(inworldx * worldW + worldX), (int)((inworldx+size) * worldW + worldY), (int)(size * worldW), (int)1), color);

        }
        /*void calcMatchRate()
        {
            int[] phaseForGPU = new int[num_markers * myphaseData[0].dataphase.Count];
            for (int i = 0; i < num_markers; i++)
            {
                for (int j = 0; j < myphaseData[0].dataphase.Count; j++)
                {
                    phaseForGPU[i * myphaseData[0].dataphase.Count + j] = myphaseData[i].dataphase[j];
                }
            }
            using Context context2 = Context.Create(builder => builder.AllAccelerators());
            Accelerator accelerator2 = context2.GetPreferredDevice(preferCPU: false).CreateAccelerator(context2);
            accelerator2.PrintInformation();
            MemoryBuffer1D<int, Stride1D.Dense> deviceData2 = accelerator2.Allocate1D(phaseForGPU);
            MemoryBuffer1D<float, Stride1D.Dense> deviceOutput2 = accelerator2.Allocate1D<float>(num_markers * num_markers);
            Action<Index2D, int, int, ArrayView<int>, ArrayView<float>> loadedKernel2 =
                accelerator2.LoadAutoGroupedStreamKernel<Index2D, int, int, ArrayView<int>, ArrayView<float>>(CalcMatchRateKernel);
            loadedKernel2(new Index2D(num_markers, num_markers), num_markers, myphaseData[0].dataphase.Count, deviceData2.View, deviceOutput2.View);
            accelerator2.Synchronize();
            float[] hostOutput2 = deviceOutput2.GetAsArray1D();
            Console.WriteLine(hostOutput2.Length);
            distphase3 = new float[num_markers, num_markers];
            for (int i = 0; i < num_markers; i++)
            {
                for (int j = 0; j < num_markers; j++)
                {
                    distphase3[i, j] = hostOutput2[i * num_markers + j];
                }
            }

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Console.Write("\t" + distphase3[i, j]);
                }
                Console.WriteLine("");
            }
            accelerator2.Dispose();
            context2.Dispose();
        }*/

        List<PhaseData> updatePhaseDelete(int areaStart, int areaEnd)
        {
            List<PhaseData> tempmyphaseData = new List<PhaseData>();
            for (int i = 0; i < myphaseData.Count; i++)
            {
                if (i < areaStart || i > areaEnd)
                {
                    tempmyphaseData.Add(myphaseData[i]);
                }
            }
            return tempmyphaseData;
        }
        void updateDistanceDelete(int areaStart, int areaEnd)
        {
            float[,] tempdistphase3 = new float[num_markers - (areaEnd - areaStart + 1), num_markers - (areaEnd - areaStart + 1)];

            System.Threading.Tasks.Parallel.For(0, areaStart, j => {
                for (int i = 0; i < areaStart; i++) tempdistphase3[i, j] = distphase3[i, j];
                for (int i = areaEnd + 1; i < num_markers; i++) tempdistphase3[i - (areaEnd - areaStart + 1), j] = distphase3[i, j];
            });
            System.Threading.Tasks.Parallel.For(areaEnd+1, num_markers, j => {
                for (int i = 0; i < areaStart; i++) tempdistphase3[i, j - (areaEnd - areaStart + 1)] = distphase3[i, j];
                for (int i = areaEnd + 1; i < num_markers; i++) tempdistphase3[i - (areaEnd - areaStart + 1), j - (areaEnd - areaStart + 1)] = distphase3[i, j];
            });
            distphase3 = tempdistphase3;
            num_markers = num_markers - (areaEnd - areaStart + 1);
            texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
        }
        List<PhaseData> updatePhaseReverse(int areaStart, int areaEnd)
        {

            bool flag = true;
            List<PhaseData> tempmyphaseData = new List<PhaseData>();
            for (int i = 0; i < num_markers; i++)
            {
                if (i < areaStart || i > areaEnd)
                {
                    tempmyphaseData.Add(myphaseData[i]);
                }
                else if (flag == true)
                {
                    flag = false;
                    for (int j = num_markers - 1; j >= 0; j--)
                    {
                        if (j >= areaStart && j <= areaEnd)
                        {
                            if (myphaseData[j].chrorient == "+")
                            {
                                myphaseData[j].chrorient = "-";
                            }
                            else if (myphaseData[j].chrorient == "-")
                            {
                                myphaseData[j].chrorient = "+";
                            }
                            else
                            {
                                myphaseData[j].chrorient = "na";
                            }
                            tempmyphaseData.Add(myphaseData[j]);
                        }
                    }
                }

            }
            return tempmyphaseData;
        }
        void updateDistanceReverse(int areaStart, int areaEnd)
        {
            System.Threading.Tasks.Parallel.For(0, num_markers, j => {
                float tempf = -1;
                for (int i = areaStart; i <= areaStart + (areaEnd - areaStart) / 2; i++)
                {
                    tempf = distphase3[i, j];
                    distphase3[i, j] = distphase3[(areaEnd - (i - areaStart)), j];
                    distphase3[(areaEnd - (i - areaStart)), j] = tempf;
                }
            });
            System.Threading.Tasks.Parallel.For(0, num_markers, j => {
                float tempf = -1;
                for (int i = areaStart; i <= areaStart + (areaEnd - areaStart) / 2; i++)
                {
                    tempf = distphase3[j, i];
                    distphase3[j, i] = distphase3[j, (areaEnd - (i - areaStart))];
                    distphase3[j, (areaEnd - (i - areaStart))] = tempf;
                }
            });
        }
        List<PhaseData> updatePhaseChange(int area1Start, int area1End, int area2Start, int area2End)
        {
            bool flag = true;
            bool flag2 = true;
            List<PhaseData> tempmyphaseData = new List<PhaseData>();
            for (int i = 0; i < num_markers; i++)
            {
                if (!(i >= area1Start && i <= area1End) && !(i >= area2Start && i <= area2End))
                {
                    tempmyphaseData.Add(myphaseData[i]);
                }
                else if (flag == true && i >= area1Start && i <= area1End)
                {
                    flag = false;
                    for (int j = 0; j < num_markers; j++)
                    {
                        if (j >= area2Start && j <= area2End)
                        {
                            tempmyphaseData.Add(myphaseData[j]);
                        }
                    }
                }
                else if (flag2 == true && i >= area2Start && i <= area2End)
                {
                    flag2 = false;
                    for (int j = 0; j < num_markers; j++)
                    {
                        if (j >= area1Start && j <= area1End)
                        {
                            tempmyphaseData.Add(myphaseData[j]);
                        }
                    }
                }
            }
            return tempmyphaseData;
        }
        void updateDistanceChange(int area1Start, int area1End, int area2Start, int area2End)
        {
            System.Threading.Tasks.Parallel.For(0, num_markers, k => {
                float[] tempfa = new float[num_markers];
                bool flag = true;
                bool flag2 = true;
                int tempindex = -1;
                for (int i = 0; i < num_markers; i++)
                {
                    if (!(i >= area1Start && i <= area1End) && !(i >= area2Start && i <= area2End))
                    {
                        tempindex++;
                        tempfa[tempindex] = distphase3[i, k];
                    }
                    else if (flag == true && i >= area1Start && i <= area1End)
                    {
                        flag = false;
                        for (int j = 0; j < num_markers; j++)
                        {
                            if (j >= area2Start && j <= area2End)
                            {
                                tempindex++;
                                tempfa[tempindex] = distphase3[j, k];
                            }
                        }
                    }
                    else if (flag2 == true && i >= area2Start && i <= area2End)
                    {
                        flag2 = false;
                        for (int j = 0; j < num_markers; j++)
                        {
                            if (j >= area1Start && j <= area1End)
                            {
                                tempindex++;
                                tempfa[tempindex] = distphase3[j, k];
                            }
                        }
                    }
                }
                for (int i = 0; i < num_markers; i++)
                {
                    distphase3[i, k] = tempfa[i];
                }
            });
            System.Threading.Tasks.Parallel.For(0, num_markers, k => {
                float[] tempfa = new float[num_markers];
                bool flag = true;
                bool flag2 = true;
                int tempindex = -1;
                for (int i = 0; i < num_markers; i++)
                {
                    if (!(i >= area1Start && i <= area1End) && !(i >= area2Start && i <= area2End))
                    {
                        tempindex++;
                        tempfa[tempindex] = distphase3[k, i];
                    }
                    else if (flag == true && i >= area1Start && i <= area1End)
                    {
                        flag = false;
                        for (int j = 0; j < num_markers; j++)
                        {
                            if (j >= area2Start && j <= area2End)
                            {
                                tempindex++;
                                tempfa[tempindex] = distphase3[k, j];
                            }
                        }
                    }
                    else if (flag2 == true && i >= area2Start && i <= area2End)
                    {
                        flag2 = false;
                        for (int j = 0; j < num_markers; j++)
                        {
                            if (j >= area1Start && j <= area1End)
                            {
                                tempindex++;
                                tempfa[tempindex] = distphase3[k, j];
                            }
                        }
                    }
                }
                for (int i = 0; i < num_markers; i++)
                {
                    distphase3[k, i] = tempfa[i];
                }
            });

        }
        void setDistTexture()
        {
            var dataColors = new Color[num_markers * num_markers];
            for (int i = 0; i < num_markers; i++)
            {
                for (int j = 0; j < num_markers; j++)
                {
                    if(colorvari == 1)
                    {
                        //背景黒
                        dataColors[i * num_markers + j] = new Color((int)(255 * distphase3[i, j]), 0, 0);
                    }
                    else
                    {
                        //背景白
                        dataColors[i * num_markers + j] = new Color(255, (int)(255 * (1 - distphase3[i, j])), (int)(255 * (1 - distphase3[i, j])));
                    }
                }
            }
            texture.SetData(dataColors);
        }
        void setPosData(int x, MarkerPos pos1)
        {

            pos1.X = x;
            pos1.chrname = myphaseData[pos1.X].chr2nd;
            pos1.contigname = myphaseData[pos1.X].chrorig;
            for (int i = pos1.X; i >= 0; i--)
            {
                if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd)
                {
                    pos1.chrStart = i;
                }
                else
                {
                    break;
                }
            }
            for (int i = pos1.X; i < num_markers; i++)
            {
                if (myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd)
                {
                    pos1.chrEnd = i;
                }
                else
                {
                    break;
                }
            }
            for (int i = pos1.X; i >= 0; i--)
            {
                if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig)
                {
                    pos1.contigStart = i;
                }
                else
                {
                    break;
                }
            }
            for (int i = pos1.X; i < num_markers; i++)
            {
                if (myphaseData[i].chrorig == myphaseData[pos1.X].chrorig)
                {
                    pos1.contigEnd = i;
                }
                else
                {
                    break;
                }
            }
        }

        public static string getRevCompBp(string bp)
        {
            switch (bp)
            {
                case "A":
                    return "T";
                case "a":
                    return "t";
                case "C":
                    return "G";
                case "c":
                    return "g";
                case "G":
                    return "C";
                case "g":
                    return "c";
                case "T":
                    return "A";
                case "t":
                    return "a";
                case "n":
                    return "n";
                default:
                    return "N";
            }
        }
        public static string getRevComp(string seq)
        {
            StringBuilder sb1 = new StringBuilder();
            for (int i = seq.Length - 1; i >= 0; i--)
            {
                sb1.Append(getRevCompBp(seq.Substring(i, 1)));
            }
            return sb1.ToString();
        }
        public StringBuilder getNstr(int n)
        {
            var strN = new StringBuilder();
            for (int i = 0; i < n; i++) strN.Append("N");
            return strN;
        }
    }
}
