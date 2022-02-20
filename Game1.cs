using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Runtime;

namespace SELDLA_G
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        Texture2D whiteRectangle;
        int[] imgData;
        Texture2D texture;
        Texture2D texture2;
        Vector2? startPosition = null;
        Vector2? deltaPosition = null;
        int worldX = 0;
        int worldY = 0;
        double worldW = 0;
        float[,] distphase3;
        int num_markers;


        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Regex reg = new Regex("^[^#]");
            List<PhaseData> cleaned  = File.ReadLines("seldla2nd_chain.ld2imp.all.txt")
            //List<PhaseData> cleaned = File.ReadLines("savedate.txt")
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

            int[] phaseForGPU = new int[cleaned.Count*cleaned[0].dataphase.Count];
            for(int i = 0; i < cleaned.Count; i++)
            {
                for(int j=0; j < cleaned[0].dataphase.Count; j++)
                {
                    phaseForGPU[i * cleaned[0].dataphase.Count + j] = cleaned[i].dataphase[j];
                }
            }
            //Console.WriteLine(phaseForGPU.Length + ", " + phaseForGPU[0]);
            // Load the data.
            MemoryBuffer1D<int, Stride1D.Dense> deviceData = accelerator.Allocate1D(phaseForGPU);
            MemoryBuffer1D<int, Stride1D.Dense> deviceOutput = accelerator.Allocate1D<int>(cleaned.Count*cleaned.Count);            
            
            // load / precompile the kernel
            Action<Index2D, int, int, ArrayView<int>, ArrayView<int>> loadedKernel =
                accelerator.LoadAutoGroupedStreamKernel<Index2D, int, int, ArrayView<int>, ArrayView<int>>(CalcMatchNumKernel);

            // finish compiling and tell the accelerator to start computing the kernel
            loadedKernel(new Index2D(cleaned.Count,cleaned.Count), cleaned.Count, cleaned[0].dataphase.Count, deviceData.View, deviceOutput.View);

            // wait for the accelerator to be finished with whatever it's doing
            // in this case it just waits for the kernel to finish.
            accelerator.Synchronize();

            // moved output data from the GPU to the CPU for output to console
            int[] hostOutput = deviceOutput.GetAsArray1D();
            Console.WriteLine(hostOutput.Length);
            int[,] distphase = new int[cleaned.Count, cleaned.Count];
            for (int i = 0; i < cleaned.Count; i++)
            {
                for (int j = 0; j < cleaned.Count; j++)
                {
                    distphase[i,j]=hostOutput[i*cleaned.Count+j];
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
            MemoryBuffer1D<int, Stride1D.Dense> deviceOutput2 = accelerator2.Allocate1D<int>(cleaned.Count * cleaned.Count);
            Action<Index2D, int, int, ArrayView<int>, ArrayView<int>> loadedKernel2 =
                accelerator2.LoadAutoGroupedStreamKernel<Index2D, int, int, ArrayView<int>, ArrayView<int>>(CalcNotNANumKernel);
            loadedKernel2(new Index2D(cleaned.Count, cleaned.Count), cleaned.Count, cleaned[0].dataphase.Count, deviceData2.View, deviceOutput2.View);
            accelerator2.Synchronize();
            int[] hostOutput2 = deviceOutput2.GetAsArray1D();
            Console.WriteLine(hostOutput2.Length);
            float[,] distphase2 = new float[cleaned.Count, cleaned.Count];
            for (int i = 0; i < cleaned.Count; i++)
            {
                for (int j = 0; j < cleaned.Count; j++)
                {
                    //if(hostOutput2[i * cleaned.Count + j] == 0)
                    //{
                    //    Console.WriteLine(i + ", " + j);
                    //}
                    distphase2[i, j] = hostOutput2[i * cleaned.Count + j];
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

            num_markers = cleaned.Count;
            distphase3 = new float[cleaned.Count, cleaned.Count];
            for (int i = 0; i < cleaned.Count; i++)
            {
                for (int j = 0; j < cleaned.Count; j++)
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
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

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
            Console.WriteLine(Color.Red.R);
            Console.WriteLine(Color.Green.G);
            Console.WriteLine(Color.Blue.B);
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

        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here
            Debug.WriteLine("Update:");
            var mouse = Mouse.GetState();
            Debug.WriteLine(mouse.ScrollWheelValue);
            worldW = mouse.ScrollWheelValue;
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                Debug.WriteLine(mouse.X);
                worldX = mouse.X;
                worldY = mouse.Y;
                if (startPosition == null)
                {// ドラッグ開始
                    startPosition = mouse.Position.ToVector2();
                }
                else
                {// ドラッグ中
                    deltaPosition = mouse.Position.ToVector2();
                }
            }
            else
            {
                startPosition = null;
                deltaPosition = null;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);

            // TODO: Add your drawing code here
            Debug.WriteLine("Draw:");

            _spriteBatch.Begin();
            _spriteBatch.Draw(whiteRectangle, new Rectangle(worldX, worldY, 80, 30), Color.Chocolate);
            Color color = new Color(128, 128, 128, 128);
            float mywheel = (float)Math.Pow(1.01, worldW);
            _spriteBatch.Draw(texture, new Vector2((float)worldX, (float)worldY), null, Color.White, 0.0f, Vector2.Zero, new Vector2(mywheel, mywheel), SpriteEffects.None, 0.0f);
            //_spriteBatch.Draw(texture, Vector2.Zero, color);
            //_spriteBatch.Draw(texture, new Vector2((float)worldX, (float)worldY), null, Color.White, 0.0f, Vector2.Zero, new Vector2(2.0f, 0.5f), SpriteEffects.None, 0.0f);
            //_spriteBatch.Draw(texture2, Vector2.Zero, Color.White);
            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
