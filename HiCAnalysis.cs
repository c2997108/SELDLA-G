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
using Mono.Options;
using System.Collections;

namespace SELDLA_G
{
    public class HiCAnalysis : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        Texture2D whiteRectangle;
        Texture2D texture;
        Texture2D texturePop;
        SKBitmap bitmap;
        SKCanvas canvas;
        SKPaint paintPop;
        int worldX = 0;
        int worldY = 80;
        int inworldX = 0;
        int inworldY = 0;
        double worldW = 1;
        Point? lastPos;
        Point stage = new Point(0, 0);
        int[,] countmatrix;  //カウントのRAWデータが入っている
        float[,] distphase3; //log10{windowごとのリード数*(windowサイズ/実際のwindowのサイズ[コンティグの端は既定のwindowサイズが取れないため、その補正])}
                             //               / log10(全てのwindowの中で最大のリード数)
        int num_markers;
        List<PhaseData> myphaseData; //Window単位でのコンティグ、chr情報が入っている。
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
        string savefileprefixname = "hic_output";
        int n_connect_length = 10000;
        Dictionary<string, string> seq = new Dictionary<string, string>();
        List<ContigPos> contigPositions;
        Dictionary<string, int> chrbpsize = new Dictionary<string, int>();
        Dictionary<string, float> chrcmsize = new Dictionary<string, float>();
        Dictionary<string, Dictionary<int, int>> posToIndex = new Dictionary<string, Dictionary<int, int>>();
        int windowsize = 100 * 1000;
        string fileSeq = "assembly.cleaned.fasta";
        //string fileSeq = @"E:\download\assembly.cleaned.fasta";
        //string fileSeq = "../../../cl0.92_sp0.90_ex0.60_split_seq.txt";
        //string fileAGP = "scaffolds_FINAL.agp";
        string fileAGP = "demo_tombo.agp";
        //string fileAGP = @"E:\download\scaffolds_FINAL.agp";
        //string fileBED = "alignment_iteration_1.bed";
        string fileBED = "demo_tombo.bed";
        //string fileBED = @"E:\download\alignment_iteration_1.bed";
        //string fileCalculated = "hic_output.matrix";
        string fileCalculated = "tombo0302.matrix";
        float color_fold = 3;
        float[,] backdistphase3;
        int[,] backcountmatrix;
        List<PhaseData> backmyphaseData = new List<PhaseData>();
        float[,] saveddistphase3;
        int[,] savedcountmatrix;
        List<PhaseData> savedmyphaseData = new List<PhaseData>();
        int colorvari = 2; //1: black, 2:white
        int limit_short_contig_length = 1*1000;
        bool showHelp = false;


        public HiCAnalysis(string[] args)
        {

            var p = new OptionSet() {
                {"a|agp=", "an input AGP file", v => fileAGP = v},
                {"b|bed=", "an input BED file", v => fileBED = v},
                {"m|matrix=", "a calculated matrix file", v => fileCalculated = v},
                {"f|fasta=", "an input FASTA file", v => fileSeq = v},
                {"o|output=", "output prefix [hic_output]", v => savefileprefixname = v},
                {"w|window=", "window size (bp) [100,000]", (int v) => windowsize = v},
                {"l|limit=", "the limit of the length of short contig (bp) [1,000]", (int v) => limit_short_contig_length = v},
                //VALUEをとらないオプションは以下のようにnullとの比較をしてTrue/Falseの値をとるようにする
                {"h|help", "show help.", v => showHelp = v != null}
            };
            try
            {
                var extra = p.Parse(args);
                extra.ForEach(t => Console.WriteLine("invalid parameter: " + t));
                if (extra.Count > 0)
                {
                    return;
                }
            }
            //パースに失敗した場合OptionExceptionを発生させる
            catch (OptionException e)
            {
                Console.WriteLine("Option parse error:");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `--help' for more information.");
                return;
            }
            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }


            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

        }
        void openFileAGP(string fileagp)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {

            }
            contigPositions = File.ReadLines(fileagp).AsParallel().AsOrdered().Where(line => line.Split("\t")[4] == "W").Select(line =>
            {
                var items = line.Split("\t");
                ContigPos tempcontig = new ContigPos();
                tempcontig.chrname = items[0];
                tempcontig.contigname = items[5];
                tempcontig.orientation = items[8];
                tempcontig.start_bp = Int32.Parse(items[1]);
                tempcontig.end_bp = Int32.Parse(items[2]);
                return tempcontig;
            }).ToList();
            Console.WriteLine("Total contigs: " + contigPositions.Count);

            posToIndex = new Dictionary<string, Dictionary<int, int>>();
            myphaseData = new List<PhaseData>();
            var chrStartIndex = new Dictionary<string, int>();
            int i = -1;
            int i2 = -1;
            contigPositions.ForEach(contig =>
            {
                int contigsize = contig.end_bp - contig.start_bp + 1;
                if(contigsize >= limit_short_contig_length)
                {
                    i2++;
                    i++;
                    int tempstart = i;
                    int tempend = i + (int)((contigsize - 1) / windowsize);
                    i = tempend;
                    if (!chrStartIndex.ContainsKey(contig.chrname)) chrStartIndex.Add(contig.chrname, tempstart);
                    if (!posToIndex.ContainsKey(contig.contigname))
                    {
                        posToIndex.Add(contig.contigname, new Dictionary<int, int>());
                        for (int j = 0; j < contigsize / (float)windowsize; j++)
                        {
                            if (contig.orientation == "+")
                            {
                                posToIndex[contig.contigname].Add(j, tempstart + j);
                            }
                            else
                            {
                                posToIndex[contig.contigname].Add(j, tempstart + (contigsize - 1) / windowsize - j);
                            }
                        }
                    }
                    for (int j = 0; j < (contigsize) / (float)windowsize; j++)
                    {
                        PhaseData tempphase = new PhaseData();
                        tempphase.chr2nd = contig.chrname;
                        tempphase.chrorig = contig.contigname;
                        tempphase.chrorient = contig.orientation;
                        if (contig.orientation == "+")
                        {
                            tempphase.markerpos = (1 + windowsize * j).ToString();
                        }
                        else
                        {
                            tempphase.markerpos = (1 + windowsize * ((contigsize - 1) / windowsize - j)).ToString();
                        }
                        tempphase.chrorigStartIndex = tempstart;
                        tempphase.chrorigEndIndex = tempend;
                        tempphase.contigsize = contig.end_bp - contig.start_bp + 1;
                        if ((j + 1) * windowsize < tempphase.contigsize)
                        {
                            tempphase.regionsize = windowsize;
                        }
                        else
                        {
                            tempphase.regionsize = tempphase.contigsize - j * windowsize;
                        }
                        myphaseData.Add(tempphase);
                    }
                }
            });
            Console.WriteLine("Over than "+limit_short_contig_length+" bp: "+(i2+1)+", Column number: " + myphaseData.Count);
            num_markers = myphaseData.Count;
        }
        void openFileBED(string fileagp, string filebed)
        {
            openFileAGP(fileagp);

            distphase3 = new float[num_markers, num_markers];

            countmatrix = new int[myphaseData.Count, myphaseData.Count];
            int count = 0;
            string oldreadname = "";
            bool iswaiting = false;
            int oldpos = -1;
            string oldcontigname = "";
            string tempreadname;
            string tempreadfs;
            int tempx;
            int tempy;
            int countValidReads = 0;
            foreach (string line in System.IO.File.ReadLines(filebed))
            {
                count++;
                if (count % (1000 * 1000) == 0) Console.WriteLine(count);
                //if (count > 10 * 1000 * 1000) break;
                var items = line.Split("\t");
                if (items.Length == 4)
                {
                    tempreadname = items[3].Substring(0, items[3].Length - 1); //SALSAの出力でリードのF, Rで/1, /2となっているので、最後の1文字を削除。
                    tempreadfs = items[3].Substring(items[3].Length - 1);
                    if (tempreadname == oldreadname)
                    {
                        if (iswaiting && tempreadfs == "2")
                        {
                            countValidReads++;
                            iswaiting = false;
                            if (posToIndex.ContainsKey(oldcontigname) && posToIndex.ContainsKey(items[0]))
                            {
                                tempx = posToIndex[oldcontigname][(oldpos - 1) / windowsize];
                                tempy = posToIndex[items[0]][((int.Parse(items[2]) + int.Parse(items[1])) / 2 - 1) / windowsize];
                                countmatrix[tempx, tempy]++;
                                countmatrix[tempy, tempx] = countmatrix[tempx, tempy];
                            }

                        }
                    }
                    else
                    {
                        oldreadname = tempreadname;
                        if (tempreadfs == "1")
                        {
                            iswaiting = true;
                            oldpos = (int.Parse(items[2]) + int.Parse(items[1])) / 2;
                            oldcontigname = items[0];
                        }
                    }
                }
            }
            Console.WriteLine("Total reads: " + count);
            Console.WriteLine("Valid paired reads (half of Total reads at most): "+countValidReads);

            int maxcount = Enumerable.Range(1, myphaseData.Count).AsParallel().Max(i =>
                Enumerable.Range(1, myphaseData.Count).ToList().Max(j => countmatrix[i - 1, j - 1])
            );
            //int maxcount = countmatrix.Cast<int>().Max();
            Console.WriteLine("Maximum count of a spot: "+maxcount);
            System.Threading.Tasks.Parallel.For(0, myphaseData.Count, i =>
            {
                Enumerable.Range(0, myphaseData.Count - 1).ToList().ForEach(j => {
                    if (countmatrix[i, j] > 0)
                    {
                        distphase3[i, j] = ((float)(Math.Log(countmatrix[i, j] * (windowsize / (float)myphaseData[i].regionsize) * (windowsize / (float)myphaseData[j].regionsize), 10) / Math.Log(maxcount, 10)));
                        if (distphase3[i, j] > 1) distphase3[i, j] = 1;
                    }

                });
            });
        }

        void openFileCalculated(string fileagp, string filecalc)
        {
            openFileAGP(fileagp);

            distphase3 = new float[num_markers, num_markers];

            countmatrix = new int[myphaseData.Count, myphaseData.Count];
            int count = 0;
            int tempx;
            int tempy;
            foreach (string line in System.IO.File.ReadLines(filecalc))
            {
                count++;
                if (count % (1000*1000) == 0) Console.WriteLine(count);
                //if (count > 10*1000*1000) break;
                var items = line.Split("\t");
                if (items.Length == 5)
                {
                    {
                        tempx = posToIndex[items[0]][(int.Parse(items[1]) - 1) / windowsize];
                        tempy = posToIndex[items[2]][(int.Parse(items[3]) - 1) / windowsize];

                        countmatrix[tempx, tempy]=int.Parse(items[4]);
                    }
                }
            }

            int maxcount = Enumerable.Range(1, myphaseData.Count).AsParallel().Max(i =>
                Enumerable.Range(1, myphaseData.Count).ToList().Max(j => countmatrix[i - 1, j - 1])
            );
            //int maxcount = countmatrix.Cast<int>().Max();
            Console.WriteLine("Total non-zero dots: " + count);
            Console.WriteLine("Maximum count of a spot: " + maxcount);
            System.Threading.Tasks.Parallel.For(0, myphaseData.Count, i =>
            {
                Enumerable.Range(0, myphaseData.Count - 1).ToList().ForEach(j => {
                    if (countmatrix[i, j] > 0)
                    {
                        distphase3[i, j] = ((float)(Math.Log(countmatrix[i, j] * (windowsize / (float)myphaseData[i].regionsize) * (windowsize / (float)myphaseData[j].regionsize), 10) / Math.Log(maxcount, 10)));
                        if (distphase3[i, j] > 1) distphase3[i, j] = 1;
                    }

                });
            });

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

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            _graphics.PreferMultiSampling = false;
            _graphics.PreferredBackBufferWidth = GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsDevice.DisplayMode.Height;
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

            //openFileBED(fileAGP, fileBED);
            openFileCalculated(fileAGP, fileCalculated);

            texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
            setDistTexture();

            var w = GraphicsDevice.DisplayMode.Width; //500;
            var h = 80;
            bitmap = new SKBitmap(w, h);
            canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            paintPop = new SKPaint();
            paintPop.TextSize = 25;
            paintPop.Color = SKColors.Black;
            // 描画コマンド実行
            canvas.Flush();

            // SKBitmapをTexture2Dに変換
            texturePop = new Texture2D(GraphicsDevice, bitmap.Width, bitmap.Height, mipmap: false, format: SurfaceFormat.Color);
            texturePop.SetData(bitmap.Bytes);
            
        }
        void backupdata()
        {
            backdistphase3 = distphase3.Clone() as float[,];
            backcountmatrix = countmatrix.Clone() as int[,];
            backmyphaseData = new List<PhaseData>();
            for (int i = 0; i < myphaseData.Count; i++)
            {
                backmyphaseData.Add(myphaseData[i].DeepCopy());
            }
        }
        void tempsavedata()
        {
            saveddistphase3 = distphase3.Clone() as float[,];
            savedcountmatrix = countmatrix.Clone() as int[,];
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
            canvas.DrawText("log10(Reads)/log10(Max Reads): " +distphase3[pos1.X, pos2.X], 10, 75, paintPop);
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

            if (state.IsKeyDown(Keys.D1) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAuto(30);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D2) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAuto(100);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D3) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAuto(300);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D4) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAuto(1000);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D6) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAutoContig(myphaseData[pos1.X].chr2nd, 30);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D6) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAutoContig(myphaseData[pos1.X].chr2nd, 100);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D6) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAutoContig(myphaseData[pos1.X].chr2nd, 300);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.D6) && changing == false)
            {
                changing = true;
                backupdata();
                updateDistanceByAutoContig(myphaseData[pos1.X].chr2nd, 1000);
                updatePhaseIndex();
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.Z) && changing == false)
            {
                changing = true;

                if (backmyphaseData.Count > 0)
                {
                    myphaseData = new List<PhaseData>();
                    for (int i = 0; i < backmyphaseData.Count; i++)
                    {
                        myphaseData.Add(backmyphaseData[i].DeepCopy());
                    }
                    countmatrix = backcountmatrix.Clone() as int[,];
                    distphase3 = backdistphase3.Clone() as float[,];
                    num_markers = myphaseData.Count;
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    //backmyphaseData = new List<PhaseData>();
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
                    myphaseData = new List<PhaseData>();
                    for (int i = 0; i < savedmyphaseData.Count; i++)
                    {
                        myphaseData.Add(savedmyphaseData[i].DeepCopy());
                    }
                    countmatrix = savedcountmatrix.Clone() as int[,];
                    distphase3 = saveddistphase3.Clone() as float[,];
                    num_markers = myphaseData.Count;
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    //savedmyphaseData = new List<PhaseData>();
                    //tempsavedata();
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
                Console.WriteLine("Enter the name of the AGP file you want to open. [\""+fileAGP+ "\"]");
                var str = Console.ReadLine();
                if (str != "") { fileAGP = str; }
                Console.WriteLine("Enter the name of the BED file you want to open. [\"" + fileBED + "\"]");
                str = Console.ReadLine();
                if (str != "") { fileBED = str; }
                try
                {
                    backupdata();
                    openFileBED(fileAGP, fileBED);
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    //calcMatchRate1line();
                    setDistTexture();

                }catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

            if (state.IsKeyDown(Keys.I) && changing == false)
            {
                changing = true;
                Console.WriteLine("Enter the name of the AGP file you want to open. [\"" + fileAGP + "\"]");
                var str = Console.ReadLine();
                if (str != "") { fileAGP = str; }
                Console.WriteLine("Enter the name of the calculated matrix file you want to open. [\"" + fileCalculated + "\"]");
                str = Console.ReadLine();
                if (str != "") { fileCalculated = str; }
                try
                {
                    backupdata();
                    openFileCalculated(fileAGP, fileCalculated);
                    texture = new Texture2D(GraphicsDevice, num_markers, num_markers);
                    //calcMatchRate1line();
                    setDistTexture();

                }
                catch (Exception ex)
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

                if (colorvari == 1)
                {
                    colorvari = 2;
                }
                else
                {
                    colorvari = 1;
                }
                setDistTexture();
            }

            if (state.IsKeyDown(Keys.B) && changing == false)
            {
                changing = true;

                Console.WriteLine("Current color intensity amplification: " + color_fold + "");
                Console.WriteLine("Enter new color intensity. [" + color_fold + "]");
                var str = Console.ReadLine();
                if (str != "") { color_fold = float.Parse(str); }

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

            if (colorvari == 1)
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
            chrbpsize = new Dictionary<string, int>();
            chrcmsize = new Dictionary<string, float>();
            contigPositions = new List<ContigPos>();

            //FASTAファイル読み出し
            var tempseq = new StringBuilder();
            string tempseqname = "";
            foreach (string line in System.IO.File.ReadLines(file))
            {
                if (line.StartsWith(">"))
                {
                    if(tempseqname != "")
                    {
                        seq.Add(tempseqname, tempseq.ToString());
                    }
                    tempseq = new StringBuilder();
                    tempseqname = line.Split(" ")[0].Split("\t")[0].Replace("\r", "").Replace("\n", "");
                    tempseqname = tempseqname.Substring(1);
                }
                else
                {
                    tempseq.Append(line.Replace("\r", "").Replace("\n", ""));
                }
            }
            seq.Add(tempseqname, tempseq.ToString());

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
                        var tempsbNA = new StringBuilder();
                        tempsbNA.Append(seq[phase.chrorig]);
                        if (phase.chrorient == "+")
                        {
                            extendedSeq.Add(phase.chr2nd, tempsb);
                            extendedSeqNAexcludedChr.Add(phase.chr2nd, tempsbNA);
                        }
                        else if (phase.chrorient == "-")
                        {
                            string revseq = getRevComp(tempsb.ToString());
                            extendedSeq.Add(phase.chr2nd, new StringBuilder(revseq));
                            string revseqNA = getRevComp(tempsbNA.ToString());
                            extendedSeqNAexcludedChr.Add(phase.chr2nd, new StringBuilder(revseqNA));
                        }
                        else //"na"
                        {
                            extendedSeq.Add(phase.chr2nd, tempsb);
                            extendedSeqNAexcludedChr.Add(phase.chr2nd, getNstr(tempsbNA.Length));
                            extendedSeqNAexcludedChr.Add(phase.chr2nd + "_related_" + phase.chrorig, tempsbNA);
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

            //drawGraph();
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

            Console.WriteLine("Saving to " + savefileprefixname + ".matrix");
            using (var fs = new System.IO.StreamWriter(savefileprefixname + ".matrix", false))
            {
                for (int i = 0; i < num_markers; i++)
                {
                    for (int j = 0; j < num_markers; j++)
                    {
                        if(countmatrix[i,j]!=0) fs.WriteLine(myphaseData[i].chrorig + "\t" + myphaseData[i].markerpos + "\t" + myphaseData[j].chrorig + "\t" + myphaseData[j].markerpos + "\t" + countmatrix[i, j]);
                    }
                }
            }
            Console.WriteLine("Saving to " + savefileprefixname + ".agp");
            using (var fs = new System.IO.StreamWriter(savefileprefixname + ".agp", false))
            {
                string oldchr = "";
                int j = 0;
                int oldpos = 0;
                string oldcontig = "";
                for (int i = 0; i < num_markers; i++)
                {
                    if(myphaseData[i].chrorig != oldcontig)
                    {
                        oldcontig = myphaseData[i].chrorig;
                        if (myphaseData[i].chr2nd != oldchr)
                        {
                            j = 1;
                            oldchr = myphaseData[i].chr2nd;
                            fs.WriteLine(myphaseData[i].chr2nd + "\t1\t" + myphaseData[i].contigsize + "\t" + j + "\tW\t" + myphaseData[i].chrorig + "\t1\t" + myphaseData[i].contigsize + "\t" + myphaseData[i].chrorient);
                            oldpos = myphaseData[i].contigsize;
                        }
                        else
                        {
                            j++;
                            fs.WriteLine(myphaseData[i].chr2nd + "\t" + (oldpos + 1) + "\t" + (oldpos + myphaseData[i].contigsize) + "\t" + j + "\tW\t" + myphaseData[i].chrorig + "\t1\t" + myphaseData[i].contigsize + "\t" + myphaseData[i].chrorient);
                            oldpos += myphaseData[i].contigsize;
                        }
                    }
                }
            }
            try
            {
                texture.SaveAsPng(File.Create(savefileprefixname + ".contactmap.png"), num_markers, num_markers);
            }catch (Exception ex)
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
            int[,] tempcountmatrix = new int[num_markers - (areaEnd - areaStart + 1), num_markers - (areaEnd - areaStart + 1)];

            System.Threading.Tasks.Parallel.For(0, areaStart, j => {
                for (int i = 0; i < areaStart; i++)
                {
                    tempdistphase3[i, j] = distphase3[i, j];
                    tempcountmatrix[i, j] = countmatrix[i, j];
                }
                for (int i = areaEnd + 1; i < num_markers; i++)
                {
                    tempdistphase3[i - (areaEnd - areaStart + 1), j] = distphase3[i, j];
                    tempcountmatrix[i - (areaEnd - areaStart + 1), j] = countmatrix[i, j];
                }
            });
            System.Threading.Tasks.Parallel.For(areaEnd+1, num_markers, j => {
                for (int i = 0; i < areaStart; i++)
                {
                    tempdistphase3[i, j - (areaEnd - areaStart + 1)] = distphase3[i, j];
                    tempcountmatrix[i, j - (areaEnd - areaStart + 1)] = countmatrix[i, j];
                }
                for (int i = areaEnd + 1; i < num_markers; i++)
                {
                    tempdistphase3[i - (areaEnd - areaStart + 1), j - (areaEnd - areaStart + 1)] = distphase3[i, j];
                    tempcountmatrix[i - (areaEnd - areaStart + 1), j - (areaEnd - areaStart + 1)] = countmatrix[i, j];
                }
            });
            distphase3 = tempdistphase3;
            countmatrix = tempcountmatrix;
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
        void updateDistanceByAuto(int num)
        {
            //int num = 30;
            string sep = "##SELDLA##";
            //染色体端の値にアクセスしやすいように連想配列を作る
            Dictionary<string, CountBox> boxOfEdges = new Dictionary<string, CountBox>();
            for (int i = 0; i < num_markers; i++)
            {
                if (!boxOfEdges.ContainsKey(myphaseData[i].chr2nd))
                {
                    boxOfEdges.Add(myphaseData[i].chr2nd, new CountBox());
                }
                boxOfEdges[myphaseData[i].chr2nd].addItem(i);
            }
            
            //染色体端間のリード数割合の平均値を計算する。染色体間のリード数割合は["chr1##SELDLA##chr2"]などのキーで取得する
            List<string> tempChrNames = new List<string>();
            myphaseData.ForEach(phaseData => tempChrNames.Add(phaseData.chr2nd));
            var ForFor = new Dictionary<string, float>();
            var ForBac = new Dictionary<string, float>();
            var BacFor = new Dictionary<string, float>();
            var BacBac = new Dictionary<string, float>();
            //tempChrNames.Distinct().AsParallel().ForAll(chrName => { //並列で処理しようとすると、Dictionaryに値を入れるときにエラーになる
            tempChrNames.Distinct().ToList().ForEach(chrName => {
                int num_x = num;
                if (boxOfEdges[chrName].getNum() < num)
                {
                    num_x = (int)((boxOfEdges[chrName].getNum()+1) / 2);
                }
                List<int> x1s = boxOfEdges[chrName].getFirstPositions(num_x);
                List<int> x2s = boxOfEdges[chrName].getLastPositions(num_x);

                tempChrNames.Distinct().ToList().ForEach(chrName2 =>
                {
                    if(chrName != chrName2)
                    {
                        int num_y = num;
                        if (boxOfEdges[chrName2].getNum() < num)
                        {
                            num_y = (int)((boxOfEdges[chrName2].getNum() + 1) / 2);
                        }
                        List<int> y1s;
                        List<int> y2s;
                        y1s = boxOfEdges[chrName2].getFirstPositions(num_y);
                        y2s = boxOfEdges[chrName2].getLastPositions(num_y);
                        int tempn;
                        float tempval;

                        tempn = 0;
                        tempval = 0;
                        x1s.ForEach(x =>
                        {
                            y1s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        ForFor[chrName + sep + chrName2] = tempval / tempn;
                        ForFor[chrName2 + sep + chrName] = tempval / tempn;

                        tempn = 0;
                        tempval = 0;
                        x1s.ForEach(x =>
                        {
                            y2s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        ForBac[chrName + sep + chrName2] = tempval / tempn;
                        BacFor[chrName2 + sep + chrName] = tempval / tempn; //BacForは結局ForBacで先に検索されてしまうので、無くても問題ないみたい

                        tempn = 0;
                        tempval = 0;
                        x2s.ForEach(x =>
                        {
                            y1s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        BacFor[chrName + sep + chrName2] = tempval / tempn; //BacForは結局ForBacで先に検索されてしまうので、無くても問題ないみたい
                        ForBac[chrName2 + sep + chrName] = tempval / tempn;

                        tempn = 0;
                        tempval = 0;
                        x2s.ForEach(x =>
                        {
                            y2s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        BacBac[chrName + sep + chrName2] = tempval / tempn;
                        BacBac[chrName2 + sep + chrName] = tempval / tempn;
                    }
                });
            });

            //値の高い染色体から順にくっつけていく。
            MaxRelation maxRelation = new MaxRelation(ForFor, ForBac, BacFor, BacBac);

            Dictionary<string, string> connectedMap = new Dictionary<string, string>();
            int countconnected = 0;
            int totalchrs = tempChrNames.Distinct().Count();
            for (int i = 0; i < totalchrs * totalchrs * 4; i++) //forfor, forbac, bacfor, bacbacの4種類を全て見る
            {
                if (!maxRelation.isEmpty())
                {
                    var (key, fr, val) = maxRelation.getTopRelation();
                    var chrs = key.Split(sep);
                    if (checkLoop(chrs[0], chrs[1], connectedMap, sep)) //ループになって繋がるとダメなので、それは避ける
                    {
                        maxRelation.getTopRelationAndDelete();
                        countconnected++;
                        Console.WriteLine(countconnected + "/" + totalchrs + " : " + val + ": " + chrs[0] + " " + chrs[1] + " " + fr);
                        if (fr == 0) //ForFor
                        {
                            connectedMap[chrs[0] + sep + "f"] = chrs[1] + sep + "f";
                            connectedMap[chrs[1] + sep + "f"] = chrs[0] + sep + "f";
                        }
                        else if (fr == 1) //ForBac
                        {
                            connectedMap[chrs[0] + sep + "f"] = chrs[1] + sep + "r";
                            connectedMap[chrs[1] + sep + "r"] = chrs[0] + sep + "f";
                        }
                        else if (fr == 2) //結局BacForの出番はなさそう
                        {
                            connectedMap[chrs[0] + sep + "r"] = chrs[1] + sep + "f";
                            connectedMap[chrs[1] + sep + "f"] = chrs[0] + sep + "r";
                        }
                        else //BacBac
                        {
                            connectedMap[chrs[0] + sep + "r"] = chrs[1] + sep + "r";
                            connectedMap[chrs[1] + sep + "r"] = chrs[0] + sep + "r";
                        }
                    }
                    else
                    {
                        maxRelation.deleteOnlyTop();
                    }
                }
            }

            //全体の並び順を決める 前側
            string keychr;
            Console.WriteLine("start with: "+myphaseData[0].chr2nd);
            Console.WriteLine("Forward...");
            Stack<string> chrforstack = new Stack<string>();
            keychr = myphaseData[0].chr2nd+sep+"f";
            for (int i = 0; i < totalchrs; i++)
            {
                if (connectedMap.ContainsKey(keychr))
                {
                    var tempstr = connectedMap[keychr].Split(sep);
                    string tempfr = tempstr[1];
                    Console.WriteLine(i+1+": "+tempstr[0]);
                    if (tempfr == "f")
                    {
                        keychr = tempstr[0] + sep + "r";
                        chrforstack.Push(tempstr[0] + sep + "rev");
                    }
                    else
                    {
                        keychr = tempstr[0] + sep + "f";
                        chrforstack.Push(tempstr[0] + sep + "for");
                    }
                }
            }
            //全体の並び順を決める 後ろ側
            Console.WriteLine("Backward...");
            Queue<string> chrbacque = new Queue<string>();
            keychr = myphaseData[0].chr2nd + sep + "r";
            for (int i = 0; i < totalchrs; i++)
            {
                if (connectedMap.ContainsKey(keychr))
                {
                    var tempstr = connectedMap[keychr].Split(sep);
                    string tempfr = tempstr[1];
                    Console.WriteLine(i+1 + ": " + tempstr[0]);
                    if (tempfr == "f")
                    {
                        keychr = tempstr[0] + sep + "r";
                        chrbacque.Enqueue(tempstr[0] + sep + "for");
                    }
                    else
                    {
                        keychr = tempstr[0] + sep + "f";
                        chrbacque.Enqueue(tempstr[0] + sep + "rev");
                    }
                }
            }

            //新しい座標を決める
            //前半
            List<int> newOrder = new List<int>();
            List<int> tempList;
            List<string> tempChrNamesAdded = new List<string>();
            List<string> tempContigOrient = new List<string>();
            while (chrforstack.Count > 0)
            {
                var key = chrforstack.Pop();
                var chrs = key.Split(sep);
                tempChrNamesAdded.Add(chrs[0]);
                if (chrs[1] == "for")
                {
                    tempList = boxOfEdges[chrs[0]].getFirstPositions(boxOfEdges[chrs[0]].getNum());
                }
                else
                {
                    tempList = boxOfEdges[chrs[0]].getLastPositions(boxOfEdges[chrs[0]].getNum());
                }
                for (int i = 0; i < tempList.Count; i++)
                {
                    newOrder.Add(tempList[i]);
                    tempContigOrient.Add(chrs[1]);
                }
            }
            //最初の染色体
            tempChrNamesAdded.Add(myphaseData[0].chr2nd);
            tempList = boxOfEdges[myphaseData[0].chr2nd].getFirstPositions(boxOfEdges[myphaseData[0].chr2nd].getNum());
            for (int i = 0; i < tempList.Count; i++)
            {
                newOrder.Add(tempList[i]);
                tempContigOrient.Add("for");
            }
            //後半
            while (chrbacque.Count > 0)
            {
                var key = chrbacque.Dequeue();
                var chrs = key.Split(sep);
                tempChrNamesAdded.Add(chrs[0]);
                if (chrs[1] == "for")
                {
                    tempList = boxOfEdges[chrs[0]].getFirstPositions(boxOfEdges[chrs[0]].getNum());
                }
                else
                {
                    tempList = boxOfEdges[chrs[0]].getLastPositions(boxOfEdges[chrs[0]].getNum());
                }
                for (int i = 0; i < tempList.Count; i++)
                {
                    newOrder.Add(tempList[i]);
                    tempContigOrient.Add(chrs[1]);
                }
            }

            //順番にしたがって、distphase3, countmatrix, myphaseDataを並び替える

            int[,] new_countmatrix = new int[num_markers, num_markers];
            float[,] new_distphase3 = new float[num_markers, num_markers];
            List<PhaseData> new_myphaseData = new List<PhaseData>();
            
            for(int i = 0; i < num_markers; i++)
            {
                //Console.WriteLine(newOrder[i]);
                new_myphaseData.Add(myphaseData[newOrder[i]]);
                if(tempContigOrient[i] != "for") //コンティグの向きの情報は自動で反転しないので、revの場合反転させる
                {
                    if (new_myphaseData[i].chrorient == "+")
                    {
                        new_myphaseData[i].chrorient = "-";
                    }
                    else if (new_myphaseData[i].chrorient == "-")
                    {
                        new_myphaseData[i].chrorient = "+";
                    }
                    else // "na"
                    {
                        new_myphaseData[i].chrorient = "na";
                    }
                }
            }

            System.Threading.Tasks.Parallel.For(0, num_markers, i => {
                for (int j = 0; j < num_markers; j++)
                {
                    new_distphase3[i, j] = distphase3[newOrder[i], newOrder[j]];
                    new_countmatrix[i, j] = countmatrix[newOrder[i], newOrder[j]];
                }
            });

            countmatrix = new_countmatrix;
            distphase3 = new_distphase3;
            myphaseData = new_myphaseData;
        }

        void updateDistanceByAutoContig(string chrToOrder, int num)
        {
            //int num = 30;
            string sep = "##SELDLA##";
            //コンティグ端の値にアクセスしやすいように連想配列を作る
            Dictionary<string, CountBox> boxOfEdges = new Dictionary<string, CountBox>();
            List<int> contigsInChr = new List<int>();
            List<string> tempContigNames = new List<string>();
            int firstContigId = -1;
            for (int i = 0; i < num_markers; i++)
            {
                if (myphaseData[i].chr2nd == chrToOrder)
                {
                    if (firstContigId == -1) firstContigId = i;
                    contigsInChr.Add(i);
                    tempContigNames.Add(myphaseData[i].chrorig);

                    if (!boxOfEdges.ContainsKey(myphaseData[i].chrorig))
                    {
                        boxOfEdges.Add(myphaseData[i].chrorig, new CountBox());
                    }
                    boxOfEdges[myphaseData[i].chrorig].addItem(i);
                }
            }

            //コンティグ端間のリード数割合の平均値を計算する。コンティグ間のリード数割合は["chr1##SELDLA##chr2"]などのキーで取得する
            var ForFor = new Dictionary<string, float>();
            var ForBac = new Dictionary<string, float>();
            var BacFor = new Dictionary<string, float>();
            var BacBac = new Dictionary<string, float>();
            //tempChrNames.Distinct().AsParallel().ForAll(chrName => { //並列で処理しようとすると、Dictionaryに値を入れるときにエラーになる
            tempContigNames.Distinct().ToList().ForEach(contigName => {
                int num_x = num;
                if (boxOfEdges[contigName].getNum() < num)
                {
                    num_x = (int)((boxOfEdges[contigName].getNum() + 1) / 2);
                }
                List<int> x1s = boxOfEdges[contigName].getFirstPositions(num_x);
                List<int> x2s = boxOfEdges[contigName].getLastPositions(num_x);

                tempContigNames.Distinct().ToList().ForEach(contigName2 =>
                {
                    if (contigName != contigName2)
                    {
                        int num_y = num;
                        if (boxOfEdges[contigName2].getNum() < num)
                        {
                            num_y = (int)((boxOfEdges[contigName2].getNum() + 1) / 2);
                        }
                        List<int> y1s;
                        List<int> y2s;
                        y1s = boxOfEdges[contigName2].getFirstPositions(num_y);
                        y2s = boxOfEdges[contigName2].getLastPositions(num_y);
                        int tempn;
                        float tempval;

                        tempn = 0;
                        tempval = 0;
                        x1s.ForEach(x =>
                        {
                            y1s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        ForFor[contigName + sep + contigName2] = tempval / tempn;
                        ForFor[contigName2 + sep + contigName] = tempval / tempn;

                        tempn = 0;
                        tempval = 0;
                        x1s.ForEach(x =>
                        {
                            y2s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        ForBac[contigName + sep + contigName2] = tempval / tempn;
                        BacFor[contigName2 + sep + contigName] = tempval / tempn; //BacForは結局ForBacで先に検索されてしまうので、無くても問題ないみたい

                        tempn = 0;
                        tempval = 0;
                        x2s.ForEach(x =>
                        {
                            y1s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        BacFor[contigName + sep + contigName2] = tempval / tempn; //BacForは結局ForBacで先に検索されてしまうので、無くても問題ないみたい
                        ForBac[contigName2 + sep + contigName] = tempval / tempn;

                        tempn = 0;
                        tempval = 0;
                        x2s.ForEach(x =>
                        {
                            y2s.ForEach(y =>
                            {
                                tempn++;
                                tempval += distphase3[x, y];
                            });
                        });
                        BacBac[contigName + sep + contigName2] = tempval / tempn;
                        BacBac[contigName2 + sep + contigName] = tempval / tempn;
                    }
                });
            });

            //値の高い染色体から順にくっつけていく。
            MaxRelation maxRelation = new MaxRelation(ForFor, ForBac, BacFor, BacBac);

            Dictionary<string, string> connectedMap = new Dictionary<string, string>();
            int countconnected = 0;
            int totalContigs = tempContigNames.Distinct().Count();
            for (int i = 0; i < totalContigs * totalContigs * 4; i++) //forfor, forbac, bacfor, bacbacの4種類を全て見る
            {
                if (!maxRelation.isEmpty()) //おそらくEmptyになることはないはずだけどif文を一応付けている
                {
                    var (key, fr, val) = maxRelation.getTopRelation();
                    var contigs = key.Split(sep);
                    if (checkLoop(contigs[0], contigs[1], connectedMap, sep)) //ループになって繋がるとダメなので、それは避ける
                    {
                        maxRelation.getTopRelationAndDelete();
                        countconnected++;
                        Console.WriteLine(countconnected + "/" + totalContigs + " : " + val + ": " + contigs[0] + " " + contigs[1] + " " + fr);
                        if (fr == 0) //ForFor
                        {
                            connectedMap[contigs[0] + sep + "f"] = contigs[1] + sep + "f";
                            connectedMap[contigs[1] + sep + "f"] = contigs[0] + sep + "f";
                        }
                        else if (fr == 1) //ForBac
                        {
                            connectedMap[contigs[0] + sep + "f"] = contigs[1] + sep + "r";
                            connectedMap[contigs[1] + sep + "r"] = contigs[0] + sep + "f";
                        }
                        else if (fr == 2) //結局BacForの出番はなさそう
                        {
                            connectedMap[contigs[0] + sep + "r"] = contigs[1] + sep + "f";
                            connectedMap[contigs[1] + sep + "f"] = contigs[0] + sep + "r";
                        }
                        else //BacBac
                        {
                            connectedMap[contigs[0] + sep + "r"] = contigs[1] + sep + "r";
                            connectedMap[contigs[1] + sep + "r"] = contigs[0] + sep + "r";
                        }
                    }
                    else
                    {
                        maxRelation.deleteOnlyTop();
                    }
                }
            }

            //全体の並び順を決める 前側
            string keyContig;
            Console.WriteLine("start with: " + myphaseData[firstContigId].chrorig);
            Console.WriteLine("Forward...");
            Stack<string> contigForStack = new Stack<string>();
            keyContig = myphaseData[firstContigId].chrorig + sep + "f";
            for (int i = 0; i < totalContigs; i++)
            {
                if (connectedMap.ContainsKey(keyContig))
                {
                    var tempstr = connectedMap[keyContig].Split(sep);
                    string tempfr = tempstr[1];
                    Console.WriteLine(i + 1 + ": " + tempstr[0]);
                    if (tempfr == "f")
                    {
                        keyContig = tempstr[0] + sep + "r";
                        contigForStack.Push(tempstr[0] + sep + "rev");
                    }
                    else
                    {
                        keyContig = tempstr[0] + sep + "f";
                        contigForStack.Push(tempstr[0] + sep + "for");
                    }
                }
            }
            //全体の並び順を決める 後ろ側
            Console.WriteLine("Backward...");
            Queue<string> contigBacQue = new Queue<string>();
            keyContig = myphaseData[firstContigId].chrorig + sep + "r";
            for (int i = 0; i < totalContigs; i++)
            {
                if (connectedMap.ContainsKey(keyContig))
                {
                    var tempstr = connectedMap[keyContig].Split(sep);
                    string tempfr = tempstr[1];
                    Console.WriteLine(i + 1 + ": " + tempstr[0]);
                    if (tempfr == "f")
                    {
                        keyContig = tempstr[0] + sep + "r";
                        contigBacQue.Enqueue(tempstr[0] + sep + "for");
                    }
                    else
                    {
                        keyContig = tempstr[0] + sep + "f";
                        contigBacQue.Enqueue(tempstr[0] + sep + "rev");
                    }
                }
            }

            //新しい座標を決める
            //前半
            List<int> newOrder = new List<int>();
            List<int> tempList;
            List<string> tempContigNamesAdded = new List<string>();
            List<string> tempContigOrient = new List<string>();
            while (contigForStack.Count > 0)
            {
                var key = contigForStack.Pop();
                var contigs = key.Split(sep);
                tempContigNamesAdded.Add(contigs[0]);
                if (contigs[1] == "for")
                {
                    tempList = boxOfEdges[contigs[0]].getFirstPositions(boxOfEdges[contigs[0]].getNum());
                }
                else
                {
                    tempList = boxOfEdges[contigs[0]].getLastPositions(boxOfEdges[contigs[0]].getNum());
                }
                for (int i = 0; i < tempList.Count; i++)
                {
                    newOrder.Add(tempList[i]);
                    tempContigOrient.Add(contigs[1]);
                }
            }
            //最初の染色体
            tempContigNamesAdded.Add(myphaseData[firstContigId].chrorig);
            tempList = boxOfEdges[myphaseData[firstContigId].chrorig].getFirstPositions(boxOfEdges[myphaseData[firstContigId].chrorig].getNum());
            for (int i = 0; i < tempList.Count; i++)
            {
                newOrder.Add(tempList[i]);
                tempContigOrient.Add("for");
            }
            //後半
            while (contigBacQue.Count > 0)
            {
                var key = contigBacQue.Dequeue();
                var contigs = key.Split(sep);
                tempContigNamesAdded.Add(contigs[0]);
                if (contigs[1] == "for")
                {
                    tempList = boxOfEdges[contigs[0]].getFirstPositions(boxOfEdges[contigs[0]].getNum());
                }
                else
                {
                    tempList = boxOfEdges[contigs[0]].getLastPositions(boxOfEdges[contigs[0]].getNum());
                }
                for (int i = 0; i < tempList.Count; i++)
                {
                    newOrder.Add(tempList[i]);
                    tempContigOrient.Add(contigs[1]);
                }
            }

            //順番にしたがって、distphase3, countmatrix, myphaseDataを並び替える

            int[,] new_countmatrix = new int[num_markers, num_markers];
            float[,] new_distphase3 = new float[num_markers, num_markers];
            List<PhaseData> new_myphaseData = new List<PhaseData>();


            List<int> newOrder2 = new List<int>();
            List<string> tempContigOrient2 = new List<string>();
            int tempCntI = -1; //chrの時は全部並び替えるのでnum_markers == newOrder.Countだったけど、contigの時は一致しないので別のindexを準備しておく
            for (int i = 0; i < num_markers; i++)
            {
                if (contigsInChr.Contains(i))
                {
                    tempCntI++;
                    newOrder2.Add(newOrder[tempCntI]);
                    tempContigOrient2.Add(tempContigOrient[tempCntI]);
                }
                else
                {
                    newOrder2.Add(i);
                    tempContigOrient2.Add("for"); //対象chr以外では向きは変更しないのでforを入れておく
                }
                new_myphaseData.Add(myphaseData[newOrder2[i]]);
                if (tempContigOrient2[i] != "for") //コンティグの向きの情報は自動で反転しないので、revの場合反転させる
                {
                    if (new_myphaseData[i].chrorient == "+")
                    {
                        new_myphaseData[i].chrorient = "-";
                    }
                    else if (new_myphaseData[i].chrorient == "-")
                    {
                        new_myphaseData[i].chrorient = "+";
                    }
                    else // "na"
                    {
                        new_myphaseData[i].chrorient = "na";
                    }
                }
            }
            System.Threading.Tasks.Parallel.For(0, num_markers, i => {
                for (int j = 0; j < num_markers; j++)
                {
                    new_distphase3[i, j] = distphase3[newOrder2[i], newOrder2[j]];
                    new_countmatrix[i, j] = countmatrix[newOrder2[i], newOrder2[j]];
                }
            });



            countmatrix = new_countmatrix;
            distphase3 = new_distphase3;
            myphaseData = new_myphaseData;
        }

        bool checkLoop(string chr1, string chr2, Dictionary<string, string> connectedMap, string sep)
        {
            if (checkLoopKey(chr1+sep+"f",chr2,connectedMap,sep)&&checkLoopKey(chr1+sep+"r",chr2,connectedMap, sep))
            {
                return true;
            }
            else
            {
                return false;
            }
            
        }
        bool checkLoopKey(string key1, string chr2, Dictionary<string, string> connectedMap, string sep)
        {
            if (!connectedMap.ContainsKey(key1))
            {
                return true;
            }
            else
            {
                var temp = connectedMap[key1].Split(sep);
                if (temp[0] == chr2)
                {
                    return false;
                }
                if (temp[1] == "f")
                {
                    return checkLoopKey(temp[0] + sep + "r", chr2, connectedMap, sep);
                }
                else
                {
                    return checkLoopKey(temp[0] + sep + "f", chr2, connectedMap, sep);
                }
            }
        }

        void updateDistanceReverse(int areaStart, int areaEnd)
        {
            System.Threading.Tasks.Parallel.For(0, num_markers, j => {
                float tempf = -1;
                int tempf2 = -1;
                for (int i = areaStart; i <= areaStart + (areaEnd - areaStart) / 2; i++)
                {
                    tempf = distphase3[i, j];
                    distphase3[i, j] = distphase3[(areaEnd - (i - areaStart)), j];
                    distphase3[(areaEnd - (i - areaStart)), j] = tempf;
                    tempf2 = countmatrix[i, j];
                    countmatrix[i, j] = countmatrix[(areaEnd - (i - areaStart)), j];
                    countmatrix[(areaEnd - (i - areaStart)), j] = tempf2;
                }
            });
            System.Threading.Tasks.Parallel.For(0, num_markers, j => {
                float tempf = -1;
                int tempf2 = -1;
                for (int i = areaStart; i <= areaStart + (areaEnd - areaStart) / 2; i++)
                {
                    tempf = distphase3[j, i];
                    distphase3[j, i] = distphase3[j, (areaEnd - (i - areaStart))];
                    distphase3[j, (areaEnd - (i - areaStart))] = tempf;
                    tempf2 = countmatrix[j, i];
                    countmatrix[j, i] = countmatrix[j, (areaEnd - (i - areaStart))];
                    countmatrix[j, (areaEnd - (i - areaStart))] = tempf2;
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
                int[] tempfa2 = new int[num_markers];
                bool flag = true;
                bool flag2 = true;
                int tempindex = -1;
                for (int i = 0; i < num_markers; i++)
                {
                    if (!(i >= area1Start && i <= area1End) && !(i >= area2Start && i <= area2End))
                    {
                        tempindex++;
                        tempfa[tempindex] = distphase3[i, k];
                        tempfa2[tempindex] = countmatrix[i, k];
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
                                tempfa2[tempindex] = countmatrix[j, k];
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
                                tempfa2[tempindex] = countmatrix[j, k];
                            }
                        }
                    }
                }
                for (int i = 0; i < num_markers; i++)
                {
                    distphase3[i, k] = tempfa[i];
                    countmatrix[i, k] = tempfa2[i];
                }
            });
            System.Threading.Tasks.Parallel.For(0, num_markers, k => {
                float[] tempfa = new float[num_markers];
                int[] tempfa2 = new int[num_markers];
                bool flag = true;
                bool flag2 = true;
                int tempindex = -1;
                for (int i = 0; i < num_markers; i++)
                {
                    if (!(i >= area1Start && i <= area1End) && !(i >= area2Start && i <= area2End))
                    {
                        tempindex++;
                        tempfa[tempindex] = distphase3[k, i];
                        tempfa2[tempindex] = countmatrix[k, i];
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
                                tempfa2[tempindex] = countmatrix[k, j];
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
                                tempfa2[tempindex] = countmatrix[k, j];
                            }
                        }
                    }
                }
                for (int i = 0; i < num_markers; i++)
                {
                    distphase3[k, i] = tempfa[i];
                    countmatrix[k, i] = tempfa2[i];
                }
            });

        }
        void setDistTexture()
        {
            var dataColors = new Color[num_markers * num_markers];
            int tempcol = 0;
            for (int i = 0; i < num_markers; i++)
            {
                for (int j = 0; j < num_markers; j++)
                {
                    tempcol = (int)(color_fold * (255 * distphase3[i, j]));
                    if (tempcol > 255) tempcol = 255;
                    if (colorvari == 1)
                    {
                        //背景黒
                        dataColors[i * num_markers + j] = new Color(tempcol, 0, 0);
                    }
                    else
                    {
                        //背景白
                        dataColors[i * num_markers + j] = new Color(255, (int)(255 - tempcol), (int)(255 - tempcol));
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
