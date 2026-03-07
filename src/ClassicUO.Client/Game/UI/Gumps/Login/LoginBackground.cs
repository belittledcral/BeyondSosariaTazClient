// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace ClassicUO.Game.UI.Gumps.Login
{
    public class LoginBackground : Gump
    {
        public LoginBackground(World world) : base(world, 0, 0)
        {
            // Always use embedded Sosaria branding instead of gumpart.mul artwork
            Add(new EmbeddedLoginImage(640, 480) { AcceptKeyboardInput = false });

            // Keep the Quit button from the classic layout on older clients
            if (Client.Game.UO.Version < ClientVersion.CV_706400)
            {
                Add
                (
                    new Button(0, 0x1589, 0x158B, 0x158A)
                    {
                        X = 555,
                        Y = 4,
                        ButtonAction = ButtonAction.Activate,
                        AcceptKeyboardInput = false
                    }
                );
            }

            CanCloseWithEsc = false;
            CanCloseWithRightClick = false;
            AcceptKeyboardInput = false;

            LayerOrder = UILayer.Under;
        }

        public override void Update()
        {
            base.Update();

            if (World.Instance != null && World.Instance.InGame)
            {
                Dispose();
            }
        }

        public override void OnButtonClick(int buttonID) => Client.Game.Exit();

        private sealed class EmbeddedLoginImage : Control
        {
            private Texture2D _texture;
            private Vector3 _hue;

            public EmbeddedLoginImage(int width, int height)
            {
                Width = width;
                Height = height;
                _hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                if (_texture == null)
                {
                    byte[] bytes = Loader.GetLoginBackground().ToArray();
                    using var ms = new MemoryStream(bytes);
                    _texture = Texture2D.FromStream(Client.Game.GraphicsDevice, ms);
                }

                batcher.Draw(_texture, new Rectangle(x, y, Width, Height), _texture.Bounds, _hue);
                return true;
            }

            public override void Dispose()
            {
                _texture?.Dispose();
                _texture = null;
                base.Dispose();
            }
        }
    }
}
