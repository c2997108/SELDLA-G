using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Threading;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Runtime;
using SkiaSharp;

namespace SELDLA_G
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        Texture2D whiteRectangle;
        //int[] imgData;
        Texture2D texture;
        Texture2D texture2;
        Texture2D texturePop;
        SKBitmap bitmap;
        SKCanvas canvas;
        SKPaint paintPop;
        Vector2? startPosition = null;
        Vector2? deltaPosition = null;
        int oldmouseX = 0;
        int oldmouseY = 0;
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
        MarkerPos pos1 = new MarkerPos();
        MarkerPos pos2 = new MarkerPos();
        bool changing = false;


        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Regex reg = new Regex("^[^#]");
            myphaseData  = File.ReadLines("seldla2nd_chain.ld2imp.all.txt")
            //myphaseData = File.ReadLines("savedate.txt")
                                        .Where(c => reg.IsMatch(c)).Take(30000).AsParallel().AsOrdered()
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
                                            }else if((items[2] == "+" && items[3] == "-") || (items[2] == "-" && items[3] == "+"))
                                            {
                                                phase.chrorient="-";
                                            }
                                            else
                                            {
                                                phase.chrorient = "na";
                                            }
                                            for(int i = 6; i < items.Length; i++)
                                            {
                                                if(items[i] == "1")
                                                {
                                                    phasedata.Add(1);
                                                }else if(items[i] == "0")
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

            using Context context = Context.Create(builder => builder.AllAccelerators());
            Debug.WriteLine("Context: " + context.ToString());
            Accelerator accelerator = context.GetPreferredDevice(preferCPU: false)
                                      .CreateAccelerator(context);

            accelerator.PrintInformation();

            int[] phaseForGPU = new int[myphaseData.Count*myphaseData[0].dataphase.Count];
            for(int i = 0; i < myphaseData.Count; i++)
            {
                for(int j=0; j < myphaseData[0].dataphase.Count; j++)
                {
                    phaseForGPU[i * myphaseData[0].dataphase.Count + j] = myphaseData[i].dataphase[j];
                }
            }
            //Console.WriteLine(phaseForGPU.Length + ", " + phaseForGPU[0]);
            // Load the data.
            MemoryBuffer1D<int, Stride1D.Dense> deviceData = accelerator.Allocate1D(phaseForGPU);
            MemoryBuffer1D<int, Stride1D.Dense> deviceOutput = accelerator.Allocate1D<int>(myphaseData.Count*myphaseData.Count);            
            
            // load / precompile the kernel
            Action<Index2D, int, int, ArrayView<int>, ArrayView<int>> loadedKernel =
                accelerator.LoadAutoGroupedStreamKernel<Index2D, int, int, ArrayView<int>, ArrayView<int>>(CalcMatchNumKernel);

            // finish compiling and tell the accelerator to start computing the kernel
            loadedKernel(new Index2D(myphaseData.Count,myphaseData.Count), myphaseData.Count, myphaseData[0].dataphase.Count, deviceData.View, deviceOutput.View);

            // wait for the accelerator to be finished with whatever it's doing
            // in this case it just waits for the kernel to finish.
            accelerator.Synchronize();

            // moved output data from the GPU to the CPU for output to console
            int[] hostOutput = deviceOutput.GetAsArray1D();
            Console.WriteLine(hostOutput.Length);
            int[,] distphase = new int[myphaseData.Count, myphaseData.Count];
            for (int i = 0; i < myphaseData.Count; i++)
            {
                for (int j = 0; j < myphaseData.Count; j++)
                {
                    distphase[i,j]=hostOutput[i*myphaseData.Count+j];
                }
            }

            for (int i = 0; i < 10; i++)
            {
                for(int j = 0; j < 10; j++)
                {
                    Console.Write("\t"+distphase[i, j]);
                }
                Console.WriteLine("");
            }


            accelerator.Dispose();
            context.Dispose();
            using Context context2 = Context.Create(builder => builder.AllAccelerators());
            Accelerator accelerator2 = context2.GetPreferredDevice(preferCPU: false)
                                       .CreateAccelerator(context2);
            MemoryBuffer1D<int, Stride1D.Dense> deviceData2 = accelerator2.Allocate1D(phaseForGPU);
            MemoryBuffer1D<int, Stride1D.Dense> deviceOutput2 = accelerator2.Allocate1D<int>(myphaseData.Count * myphaseData.Count);
            Action<Index2D, int, int, ArrayView<int>, ArrayView<int>> loadedKernel2 =
                accelerator2.LoadAutoGroupedStreamKernel<Index2D, int, int, ArrayView<int>, ArrayView<int>>(CalcNotNANumKernel);
            loadedKernel2(new Index2D(myphaseData.Count, myphaseData.Count), myphaseData.Count, myphaseData[0].dataphase.Count, deviceData2.View, deviceOutput2.View);
            accelerator2.Synchronize();
            int[] hostOutput2 = deviceOutput2.GetAsArray1D();
            Console.WriteLine(hostOutput2.Length);
            float[,] distphase2 = new float[myphaseData.Count, myphaseData.Count];
            for (int i = 0; i < myphaseData.Count; i++)
            {
                for (int j = 0; j < myphaseData.Count; j++)
                {
                    //if(hostOutput2[i * cleaned.Count + j] == 0)
                    //{
                    //    Console.WriteLine(i + ", " + j);
                    //}
                    distphase2[i, j] = hostOutput2[i * myphaseData.Count + j];
                }
            }
            
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Console.Write("\t" + distphase2[i, j]);
                }
                Console.WriteLine("");
            }


            accelerator2.Dispose();
            context2.Dispose();
            //Console.WriteLine(distphase[0,0]);

            num_markers = myphaseData.Count;
            distphase3 = new float[myphaseData.Count, myphaseData.Count];
            for (int i = 0; i < myphaseData.Count; i++)
            {
                for (int j = 0; j < myphaseData.Count; j++)
                {
                    if (distphase2[i, j] == 0) { distphase3[i, j] = 0; }
                    else
                    {
                        distphase3[i, j] = (2*distphase[i, j]-distphase2[i,j]) / distphase2[i, j];
                        if (distphase3[i, j] < 0) { distphase3[i, j] = 0; }
                    }
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

        }

        static void CalcMatchNumKernel(Index2D index, int n_markers, int n_samples,  ArrayView<int> data, ArrayView<int> output)
        {
            //output[i] = data[i % data.Length];
            //output[i] = i + j;
            
            int sum1 = 0;
            int sum2 = 0;
            int i = index.X;
            int j = index.Y;
            for (int k = 0; k < n_samples; k++)
            {
                if(data[i * n_samples + k]!=0 && data[j * n_samples + k] != 0)
                {
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
            if (sum1 > sum2)
            {
                output[i * n_markers + j] = sum1;
            }
            else
            {
                output[i * n_markers + j] = sum2;
            }

        }
        static void CalcNotNANumKernel(Index2D index, int n_markers, int n_samples, ArrayView<int> data, ArrayView<int> output)
        {
            int n = 0;
            int i = index.X;
            int j = index.Y;
            for (int k = 0; k < n_samples; k++)
            {
                if (data[i * n_samples + k] != 0 && data[j * n_samples + k] != 0)
                {
                    n++;
                }
            }
            output[i * n_markers + j] = n;

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
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            _graphics.PreferredBackBufferWidth = 2000; // GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = 1000; // GraphicsDevice.DisplayMode.Height;
            //_graphics.IsFullScreen = true;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
            Debug.WriteLine("LoadContent:");

            whiteRectangle = new Texture2D(GraphicsDevice, 1, 1);
            whiteRectangle.SetData(new[] { Color.White });
            texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
            /*            imgData = new int[1000 * 1000];
                        for(int i = 0; i < imgData.Length; i++)
                        {
                            imgData[i] = i;
                        }
                        texture.SetData(imgData);
            */
            var dataColors = new Color[num_markers * num_markers];
            for (int i = 0; i < num_markers; i++)
            {
                for(int j = 0; j < num_markers; j++)
                {
                    dataColors[i * num_markers + j] = new Color((int)(255 * distphase3[i, j]), 0, 0);
                }
            }
            //texture.SetData(0, new Rectangle(0, 0, num_markers, num_markers), dataColors, 0, num_markers * num_markers);
            texture.SetData(dataColors);

            FileStream fileStream = new FileStream("seldla2nd_heatmap_phase_physical.png", FileMode.Open);
            texture2 = Texture2D.FromStream(GraphicsDevice, fileStream);
            fileStream.Dispose();
            Color[] data = new Color[texture2.Width * texture2.Height];
            texture2.GetData(data);
            for (int i = 0; i < data.Length; i++)
            {
                //if (i < 1000) { Console.WriteLine(data[i].R + " " + data[i].G + " " + data[i].B); }
                byte gray = (byte)(0.29 * data[i].R + 0.58 * data[i].G + 0.11 * data[i].B);
                data[i] = new Color(gray, gray, gray, data[i].A);
            }
            texture2.SetData(data);

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
            if(tempX < 0) { tempX = 0; } else if(tempX >= myphaseData.Count){ tempX = myphaseData.Count - 1; }
            int tempY = inworldY;
            if (tempY < 0) { tempY = 0; } else if (tempY >= myphaseData.Count) { tempY = myphaseData.Count - 1; }
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

                //if (startPosition == null)
                //{// ドラッグ開始
                //    startPosition = mouse.Position.ToVector2();
                //}
                //else
                //{// ドラッグ中
                //    deltaPosition = mouse.Position.ToVector2();
                //}
            }
            else if(mouse.LeftButton == ButtonState.Released)
            {
                stage = new Point(worldX, worldY);
                lastPos = null;
            }/*else
            {
                startPosition = null;
                deltaPosition = null;
            }*/

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
                bool flag = true;
                List<PhaseData> tempmyphaseData = new List<PhaseData>();
                for(int i = 0; i < myphaseData.Count; i++)
                {
                    if(myphaseData[i].chr2nd != myphaseData[pos1.X].chr2nd)
                    {
                        tempmyphaseData.Add(myphaseData[i]);
                    }else if(flag == true && myphaseData[i].chr2nd == myphaseData[pos1.X].chr2nd)
                    {
                        flag = false;
                        for(int j = myphaseData.Count-1; j >= 0; j--)
                        {
                            if (myphaseData[j].chr2nd == myphaseData[pos1.X].chr2nd)
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
                myphaseData = tempmyphaseData;
                calcMatchRate();
                setDistTexture();
            }
            if (state.IsKeyDown(Keys.T))
            {
                System.Threading.Thread.Sleep(2000);
            }
            if (state.IsKeyDown(Keys.Y))
            {

            }
            if (state.IsKeyDown(Keys.U))
            {

            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);

            // TODO: Add your drawing code here
            Debug.WriteLine("Draw:");

            _spriteBatch.Begin();
            //_spriteBatch.Draw(whiteRectangle, new Rectangle(worldX, worldY, 80, 30), Color.Chocolate);
            //Color color = new Color(128, 128, 128, 128);
            _spriteBatch.Draw(texture, new Vector2((float)worldX, (float)worldY), null, Color.White, 0.0f, Vector2.Zero, new Vector2((float)worldW, (float)worldW), SpriteEffects.None, 0.0f);
            //_spriteBatch.Draw(texture, Vector2.Zero, color);
            //_spriteBatch.Draw(texture, new Vector2((float)worldX, (float)worldY), null, Color.White, 0.0f, Vector2.Zero, new Vector2(2.0f, 0.5f), SpriteEffects.None, 0.0f);
            //_spriteBatch.Draw(texture2, Vector2.Zero, Color.White);
            _spriteBatch.Draw(whiteRectangle, new Rectangle((int)(inworldX * worldW + worldX), (int)(inworldX * worldW + worldY), 1, (int)(1*worldW)), new Color(255, 255, 0, 255));
            _spriteBatch.Draw(whiteRectangle, new Rectangle((int)((inworldX+1) * worldW + worldX), (int)(inworldX * worldW + worldY), 1, (int)(1 * worldW)), new Color(255, 255, 0, 255));
            _spriteBatch.Draw(whiteRectangle, new Rectangle((int)(inworldX * worldW + worldX), (int)(inworldX * worldW + worldY), (int)(1 * worldW), 1), new Color(255, 255, 0, 255));
            _spriteBatch.Draw(whiteRectangle, new Rectangle((int)(inworldX * worldW + worldX), (int)((inworldX+1) * worldW + worldY), (int)(1 * worldW), 1), new Color(255, 255, 0, 255));

            drawRect(_spriteBatch, whiteRectangle, pos1.chrStart, pos1.chrEnd - pos1.chrStart + 1, Color.Yellow);
            drawRect(_spriteBatch, whiteRectangle, pos1.contigStart, pos1.contigEnd - pos1.contigStart + 1, Color.Green);


            _spriteBatch.Draw(texturePop, new Vector2(0, 0), Color.White);

            _spriteBatch.End();
            base.Draw(gameTime);
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
            int[] phaseForGPU = new int[myphaseData.Count * myphaseData[0].dataphase.Count];
            for (int i = 0; i < myphaseData.Count; i++)
            {
                for (int j = 0; j < myphaseData[0].dataphase.Count; j++)
                {
                    phaseForGPU[i * myphaseData[0].dataphase.Count + j] = myphaseData[i].dataphase[j];
                }
            }
            using Context context2 = Context.Create(builder => builder.AllAccelerators());
            Accelerator accelerator2 = context2.GetPreferredDevice(preferCPU: false).CreateAccelerator(context2);
            MemoryBuffer1D<int, Stride1D.Dense> deviceData2 = accelerator2.Allocate1D(phaseForGPU);
            MemoryBuffer1D<float, Stride1D.Dense> deviceOutput2 = accelerator2.Allocate1D<float>(myphaseData.Count * myphaseData.Count);
            Action<Index2D, int, int, ArrayView<int>, ArrayView<float>> loadedKernel2 =
                accelerator2.LoadAutoGroupedStreamKernel<Index2D, int, int, ArrayView<int>, ArrayView<float>>(CalcMatchRateKernel);
            loadedKernel2(new Index2D(myphaseData.Count, myphaseData.Count), myphaseData.Count, myphaseData[0].dataphase.Count, deviceData2.View, deviceOutput2.View);
            accelerator2.Synchronize();
            float[] hostOutput2 = deviceOutput2.GetAsArray1D();
            Console.WriteLine(hostOutput2.Length);
            for (int i = 0; i < myphaseData.Count; i++)
            {
                for (int j = 0; j < myphaseData.Count; j++)
                {
                    distphase3[i, j] = hostOutput2[i * myphaseData.Count + j];
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
            for (int i = pos1.X; i < myphaseData.Count; i++)
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
            for (int i = pos1.X; i < myphaseData.Count; i++)
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
    }
}
