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
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        Texture2D whiteRectangle;
        //int[] imgData;
        Texture2D texture;
        //Texture2D texture2;
        Texture2D texturePop;
        SKBitmap bitmap;
        SKCanvas canvas;
        SKPaint paintPop;
        /*Vector2? startPosition = null;
        Vector2? deltaPosition = null;
        int oldmouseX = 0;
        int oldmouseY = 0;*/
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
        string savefilename = "savedata.txt";
        int n_connect_length = 10000;


        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

/*            using Context context = Context.Create(builder => builder.AllAccelerators());
            Debug.WriteLine("Context: " + context.ToString());
            Accelerator accelerator = context.GetPreferredDevice(preferCPU: false)
                                      .CreateAccelerator(context);
            accelerator.PrintInformation();*/
        }

        void openFile(string filename)
        {
            Regex reg = new Regex("^[^#]");
            //myphaseData  = File.ReadLines("../../../seldla2nd_chain.ld2imp.all.txt")

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
        }
        static void CalcMatchRateKernel(Index2D index, int n_markers, int n_samples, ArrayView<int> data, ArrayView<float> output)
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
        }
        static void CalcMatchN1lineKernel(Index1D index, int j, int n_markers, int n_samples, ArrayView<int> data, ArrayView<float> output, ArrayView<float> outputN)
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
            outputN[i] = n;
            if (n == 0)
            {
                output[i] = 0;
            }
            else
            {
                if (sum1 > sum2)
                {
                    output[i] = sum1;
                }
                else
                {
                    output[i] = sum2;
                }
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

            //openFile("savedate.txt");
            openFile("../../../savedate.txt");
            //openFile("../../../seldla2nd_chain.ld2imp.all.txt");
            //openFile("../../../seldla2nd_chain.ph.all.txt");

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

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

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

            // If they hit esc, exit
            //if (state.IsKeyDown(Keys.Escape))
            //    Exit();

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

            // Move our sprite based on arrow keys being pressed:
            if (state.IsKeyDown(Keys.R) && changing == false)
            {
                changing = true;

                updateDistanceReverse(pos1.chrStart, pos1.chrEnd);
                myphaseData = updatePhaseReverse(pos1.chrStart, pos1.chrEnd);
                //calcMatchRate1line();
                setDistTexture();
            }
            if (state.IsKeyDown(Keys.T) && changing == false)
            {
                changing = true;
                if(myphaseData[pos1.X].chr2nd != myphaseData[pos2.X].chr2nd)
                {
                    updateDistanceChange(pos1.chrStart, pos1.chrEnd, pos2.chrStart, pos2.chrEnd);
                    myphaseData = updatePhaseChange(pos1.chrStart, pos1.chrEnd, pos2.chrStart, pos2.chrEnd);
                    //calcMatchRate1line();
                    setDistTexture();

                }
            }
            if (state.IsKeyDown(Keys.Y) && changing == false)
            {
                changing = true;

                updateDistanceReverse(pos1.contigStart, pos1.contigEnd);
                myphaseData = updatePhaseReverse(pos1.contigStart, pos1.contigEnd);
                //calcMatchRate1line();
                setDistTexture();
            }
            if (state.IsKeyDown(Keys.U) && changing == false)
            {
                changing = true;
                if (myphaseData[pos1.X].chrorig != myphaseData[pos2.X].chrorig)
                {
                    updateDistanceChange(pos1.contigStart, pos1.contigEnd, pos2.contigStart, pos2.contigEnd);
                    myphaseData = updatePhaseChange(pos1.contigStart, pos1.contigEnd, pos2.contigStart, pos2.contigEnd);
                    //calcMatchRate1line();
                    setDistTexture();

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

                    updateDistanceReverse(markNstart[2], markNend[2]);
                    myphaseData = updatePhaseReverse(markNstart[2], markNend[2]);
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
                        updateDistanceChange(markMstart1[2], markMend1[2], markMstart2[2], markMend2[2]);
                        myphaseData = updatePhaseChange(markMstart1[2], markMend1[2], markMstart2[2], markMend2[2]);
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

                updateDistanceDelete(pos1.contigStart, pos1.contigEnd);
                myphaseData = updatePhaseDelete(pos1.contigStart, pos1.contigEnd);
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

            if (state.IsKeyDown(Keys.S) && changing == false)
            {
                changing = true;

                savedata();
            }

            if (state.IsKeyDown(Keys.O) && changing == false)
            {
                changing = true;
                Console.WriteLine("Enter the name of the file you want to open. [\"../../../savedata.txt\"]");
                var str = Console.ReadLine();
                try
                {
                    openFile(str);
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

                openseq("../../../cl0.92_sp0.90_ex0.60_split_seq.txt");
                
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

            drawRect(_spriteBatch, whiteRectangle, pos1.chrStart, pos1.chrEnd - pos1.chrStart + 1, Color.Yellow);
            drawRect(_spriteBatch, whiteRectangle, pos1.contigStart, pos1.contigEnd - pos1.contigStart + 1, Color.Green);
            drawRect(_spriteBatch, whiteRectangle, pos2.chrStart, pos2.chrEnd - pos2.chrStart + 1, Color.Yellow);
            drawRect(_spriteBatch, whiteRectangle, pos2.contigStart, pos2.contigEnd - pos2.contigStart + 1, Color.Green);

            if(markN == 1)
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
                    drawRect(_spriteBatch, whiteRectangle, markNstart[0],tempNend - markNstart[0] + 1,Color.Red);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempNstart,markNend[0] - tempNstart + 1,Color.Red);
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
                    drawRect(_spriteBatch, whiteRectangle, markMstart1[0], tempMend1 - markMstart1[0] + 1, Color.Red);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempMstart1, markMend1[0] - tempMstart1 + 1, Color.Red);
                }
            }else if(markM == 2)
            {
                drawRect(_spriteBatch, whiteRectangle, markMstart1[2], markMend1[2] - markMstart1[2] + 1, Color.Red);
            }else if(markM == 3)
            {
                drawRect(_spriteBatch, whiteRectangle, markMstart1[2], markMend1[2] - markMstart1[2] + 1, Color.Red);
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
                    drawRect(_spriteBatch, whiteRectangle, markMstart2[0], tempMend1 - markMstart2[0] + 1, Color.Red);
                }
                else
                {
                    drawRect(_spriteBatch, whiteRectangle, tempMstart1, markMend2[0] - tempMstart1 + 1, Color.Red);
                }
            }

            _spriteBatch.Draw(texturePop, new Vector2(0, 0), Color.White);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        void openseq(string file)
        {
            var seq = new Dictionary<string, string>();
            File.ReadLines(file).AsParallel().ForAll(line =>
            {
                var arr = line.Split("\t");
                seq.Add(arr[0], arr[1]);
            });
            Console.WriteLine(seq.Count);
            
            var extendedSeq = new Dictionary<string, StringBuilder>();
            var extendedSeqNAexcludedChr = new Dictionary<string, StringBuilder>();
            var strN = getNstr(n_connect_length);
            string oldcontigname = "";
            foreach(var phase in myphaseData)
            {
                if (phase.chrorig != oldcontigname)
                {
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
            using (var fs = new System.IO.StreamWriter(savefilename+".includeNA.fasta", false))
            {
                foreach (var item in extendedSeq)
                {
                    fs.WriteLine(">"+item.Key);
                    fs.WriteLine(item.Value);
                }
            }
            using (var fs = new System.IO.StreamWriter(savefilename + ".fasta", false))
            {
                foreach (var item in extendedSeqNAexcludedChr)
                {
                    fs.WriteLine(">" + item.Key);
                    fs.WriteLine(item.Value);
                }
            }
        }
                                        
        void savedata()
        {

            Console.WriteLine("Enter the name of the file you want to save. [\"" + savefilename + "\"]");
            var str = Console.ReadLine();
            if (str != "") { savefilename = str; }

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
            Console.WriteLine("Saving to " + savefilename);
            System.IO.File.WriteAllLines(savefilename, result);
        }
        void drawRect(SpriteBatch sprite, Texture2D rect, int inworldx, int size, Color color)
        {
            sprite.Draw(rect, new Rectangle((int)(inworldx * worldW + worldX), (int)(inworldx * worldW + worldY), (int)1, (int)(size * worldW)), color);
            sprite.Draw(rect, new Rectangle((int)((inworldx+size) * worldW + worldX), (int)(inworldx * worldW + worldY), (int)1, (int)(size * worldW)), color);
            sprite.Draw(rect, new Rectangle((int)(inworldx * worldW + worldX), (int)(inworldx * worldW + worldY), (int)(size*worldW), (int)1), color);
            sprite.Draw(rect, new Rectangle((int)(inworldx * worldW + worldX), (int)((inworldx+size) * worldW + worldY), (int)(size * worldW), (int)1), color);

        }
        void calcMatchRate()
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
        }
        void calcMatchRate1line()
        {
            distphase3 = new float[num_markers, num_markers];
            float[,] distphaseN = new float[num_markers, num_markers];
            float[,] distphaseV = new float[num_markers, num_markers];
            int[] phaseForGPU;
            using Context context2 = Context.Create(builder => builder.AllAccelerators()); //Context.Create(builder => builder.OpenCL());
            Accelerator accelerator2 = context2.GetPreferredDevice(preferCPU: false).CreateAccelerator(context2);
            accelerator2.PrintInformation();
            MemoryBuffer1D<int, Stride1D.Dense> deviceData2;
            MemoryBuffer1D<float, Stride1D.Dense> deviceOutputV = accelerator2.Allocate1D<float>(num_markers);
            MemoryBuffer1D<float, Stride1D.Dense> deviceOutputN = accelerator2.Allocate1D<float>(num_markers);
            Action<Index1D, int, int, int, ArrayView<int>, ArrayView<float>, ArrayView<float>> loadedKernel2 =
                accelerator2.LoadAutoGroupedStreamKernel<Index1D, int, int, int, ArrayView<int>, ArrayView<float>, ArrayView<float>>(CalcMatchN1lineKernel);
            int n = -1;
            foreach (var eachfamily in myfamily.Values)
            {
                n++;
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
                    loadedKernel2(new Index1D(num_markers), j, num_markers, eachfamily.Count, deviceData2.View, deviceOutputV.View, deviceOutputN.View);
                    accelerator2.Synchronize();
                    float[] hostOutputV = deviceOutputV.GetAsArray1D();
                    float[] hostOutputN = deviceOutputN.GetAsArray1D();
                    //Console.WriteLine(hostOutput2.Length);
                    for (int i = 0; i < num_markers; i++)
                    {
                        if (n == 0)
                        {
                            distphaseN[i, j] = hostOutputN[i];
                            distphaseV[i, j] = hostOutputV[i];
                        }
                        else
                        {
                            distphaseN[i, j] += hostOutputN[i];
                            distphaseV[i, j] += hostOutputV[i];
                        }
                    }
                }
            }
            accelerator2.Dispose();
            context2.Dispose();

            for (int j = 0; j < num_markers; j++)
            {
                for (int i = 0; i < num_markers; i++)
                {
                    distphase3[i, j] = 2 * distphaseV[i,j] / (float)distphaseN[i,j] - 1.0f;
                }
            }
        }

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
                    dataColors[i * num_markers + j] = new Color((int)(255 * distphase3[i, j]), 0, 0);
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
