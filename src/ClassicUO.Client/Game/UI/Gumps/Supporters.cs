using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    public class Supporters : Gump
    {
        private const int WIDTH = 400;
        private const int HEIGHT = 200;

        private AlphaBlendControl _background;

        public Supporters(World world) : base(world, 0, 0)
        {
            Width = WIDTH;
            Height = HEIGHT;
            X = (Client.Game.Window.ClientBounds.Width - Width) >> 1;
            Y = (Client.Game.Window.ClientBounds.Height - Height) >> 1;

            CanCloseWithEsc = true;
            CanCloseWithRightClick = true;
            CanMove = true;
            AcceptMouseInput = true;

            _background = new AlphaBlendControl();
            _background.Width = WIDTH;
            _background.Height = HEIGHT;
            _background.X = 1;
            _background.Y = 1;
            Add(_background);

            var title = new Label("Beyond Sosaria Client", true, 0xffff, WIDTH, 255, FontStyle.BlackBorder, Assets.TEXT_ALIGN_TYPE.TS_CENTER, true);
            title.Y = 40;
            Add(title);

            var attribution = new Label("Built on TazUO by TazmanianTad\nFounded on the ClassicUO open-source project", true, 0xffff, WIDTH, 255, FontStyle.BlackBorder, Assets.TEXT_ALIGN_TYPE.TS_CENTER, true);
            attribution.Y = title.Y + title.Height + 16;
            Add(attribution);
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            Vector3 hue = ShaderHueTranslator.GetHueVector(0);
            batcher.DrawRectangle
            (
                SolidColorTextureCache.GetTexture(Color.Gray),
                x,
                y,
                Width - 3,
                Height + 1,
                hue
            );
            return base.Draw(batcher, x, y);
        }
    }
}
