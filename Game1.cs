using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System;

namespace SELDLA_G
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        Texture2D whiteRectangle;
        int[] imgData;
        Texture2D texture;
        Vector2? startPosition = null;
        Vector2? deltaPosition = null;
        int worldX = 0;
        int worldY = 0;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
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
            texture = new Texture2D(GraphicsDevice, 1000, 1000);
            /*            imgData = new int[1000 * 1000];
                        for(int i = 0; i < imgData.Length; i++)
                        {
                            imgData[i] = i;
                        }
                        texture.SetData(imgData);
            */
            var dataColors = new Color[100 * 100];
            for (var i = 0; i < dataColors.Length; i++)
            {
                dataColors[i] = new Color(Color.Red.R, i % 225, 0);
            }
            texture.SetData(0, new Rectangle(100, 100, 100, 100), dataColors, 0, 100 * 100);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here
            Debug.WriteLine("Update:");
            var mouse = Mouse.GetState();
            Debug.WriteLine(mouse.ScrollWheelValue);
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
            _spriteBatch.Draw(texture, Vector2.Zero, color);
            _spriteBatch.Draw(texture, new Vector2((float)worldX, (float)worldY), null, Color.White, 0.0f, Vector2.Zero, new Vector2(2.0f, 0.5f), SpriteEffects.None, 0.0f);
            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
