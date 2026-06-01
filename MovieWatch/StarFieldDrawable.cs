namespace MovieWatch {
    public class StarFieldDrawable(Star[] stars) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect) {
            canvas.FillColor = Colors.White;

            foreach (var star in stars) {
                // Dynamically scale coordinates to perfectly match any device screen width/height
                var targetX = (star.X / 2000f) * dirtyRect.Width;
                var targetY = (star.Y / 2000f) * dirtyRect.Height;

                canvas.FillRectangle(targetX, targetY, 1, 1);
            }
        }
    }
}
