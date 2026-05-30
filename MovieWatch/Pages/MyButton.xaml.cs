#pragma warning disable CS0618  // suppress obsolete-API warnings (PointerGestureRecognizer etc.)

namespace MovieWatch.Pages {
    public partial class MyButton  // ContentView, not ContentPage
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(MyButton), "Button",
                propertyChanged: (b, _, n) => ((MyButton)b).ButtonLabel.Text = (string)n);

        public string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public event EventHandler? Clicked;

        public MyButton() {
            InitializeComponent();

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnTapped;
            OuterBox.GestureRecognizers.Add(tap);

            var pointer = new PointerGestureRecognizer();
            pointer.PointerPressed += OnPressed;
            pointer.PointerReleased += OnReleased;
            OuterBox.GestureRecognizers.Add(pointer);

            OuterBox.GestureRecognizers.Add(CreateTouchGesture());
        }

        private TapGestureRecognizer CreateTouchGesture() {
            var gesture = new TapGestureRecognizer();
            gesture.Tapped += async (_, _) => {
                await PressAnimation();
                await Task.Delay(100);
                await ReleaseAnimation();
            };
            return gesture;
        }

        private async Task PressAnimation() {
            OuterBox.Padding = new Thickness(0);
            OuterBox.Margin = new Thickness(0, 0, 0, 10);
            await OuterBox.TranslateTo(0, 10, 60, Easing.SinIn);
        }

        private async Task ReleaseAnimation() {
            OuterBox.Padding = new Thickness(0, 0, 0, 10);
            OuterBox.Margin = new Thickness(0);
            await OuterBox.TranslateTo(0, 0, 60, Easing.SinOut);
        }

        private void OnPressed(object? sender, PointerEventArgs e) => _ = PressAnimation();
        private void OnReleased(object? sender, PointerEventArgs e) => _ = ReleaseAnimation();
        private void OnTapped(object? sender, TappedEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
    }
}

#pragma warning restore CS0618