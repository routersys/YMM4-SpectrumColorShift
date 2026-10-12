using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SpectrumColorShift;

internal sealed class SpectrumColorShiftCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    internal enum PropertyIndex
    {
        Distance = 0,
        Mode,
        Angle,
        CenterX,
        CenterY,
        Strength,
        Dispersion,
        ReferenceWavelength,
        Steps,
    }

    public float Distance { set => SetValue((int)PropertyIndex.Distance, value); }
    public int Mode { set => SetValue((int)PropertyIndex.Mode, value); }
    public float Angle { set => SetValue((int)PropertyIndex.Angle, value); }
    public float CenterX { set => SetValue((int)PropertyIndex.CenterX, value); }
    public float CenterY { set => SetValue((int)PropertyIndex.CenterY, value); }
    public float Strength { set => SetValue((int)PropertyIndex.Strength, value); }
    public int Dispersion { set => SetValue((int)PropertyIndex.Dispersion, value); }
    public float ReferenceWavelength { set => SetValue((int)PropertyIndex.ReferenceWavelength, value); }
    public int Steps { set => SetValue((int)PropertyIndex.Steps, value); }

    [CustomEffect(1)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private ShiftMode _mode = ShiftMode.Linear;
        private DispersionLaw _dispersion = DispersionLaw.Grating;
        private Vector2 _centerOffset;
        private float _referenceWavelength = Spectrum.DefaultReferenceWavelength;
        private int _steps = Spectrum.DefaultSteps;
        private bool _tableStale = true;

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Distance)]
        public float Distance
        {
            get => _cb.Distance;
            set
            {
                _cb.Distance = Sanitize(value, -MaxInputPixel, MaxInputPixel);
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Mode)]
        public int Mode
        {
            get => (int)_mode;
            set
            {
                _mode = value is (int)ShiftMode.Radial or (int)ShiftMode.Rotational ? (ShiftMode)value : ShiftMode.Linear;
                _cb.Mode = (int)_mode;
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Angle)]
        public float Angle
        {
            get => MathF.Atan2(_cb.Direction.Y, _cb.Direction.X) * 180f / MathF.PI;
            set
            {
                var radians = float.IsFinite(value) ? value * MathF.PI / 180f : 0f;
                _cb.Direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.CenterX)]
        public float CenterX
        {
            get => _centerOffset.X;
            set
            {
                _centerOffset.X = Sanitize(value, -MaxInputPixel, MaxInputPixel);
                RefreshGeometry();
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.CenterY)]
        public float CenterY
        {
            get => _centerOffset.Y;
            set
            {
                _centerOffset.Y = Sanitize(value, -MaxInputPixel, MaxInputPixel);
                RefreshGeometry();
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Strength)]
        public float Strength
        {
            get => _cb.Strength;
            set
            {
                _cb.Strength = Sanitize(value, 0f, 1f);
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Dispersion)]
        public int Dispersion
        {
            get => (int)_dispersion;
            set
            {
                _dispersion = value == (int)DispersionLaw.Prism ? DispersionLaw.Prism : DispersionLaw.Grating;
                _tableStale = true;
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ReferenceWavelength)]
        public float ReferenceWavelength
        {
            get => _referenceWavelength;
            set
            {
                _referenceWavelength = Spectrum.ClampReference(value);
                _tableStale = true;
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Steps)]
        public int Steps
        {
            get => _steps;
            set
            {
                _steps = Spectrum.ClampSteps(value);
                _cb.Steps = _steps;
                _tableStale = true;
                UpdateConstants();
            }
        }

        public EffectImpl() : base(ShaderResourceUri.Get("SpectrumColorShift"))
        {
            _cb.Direction = Vector2.UnitX;
            _cb.Radius = 1f;
            _cb.Strength = 1f;
            _cb.Mode = (int)ShiftMode.Linear;
            _cb.Steps = Spectrum.DefaultSteps;
        }

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                if (_tableStale)
                {
                    Spectrum.Fill(_cb.Samples, _steps, _referenceWavelength, _dispersion);
                    _tableStale = false;
                }

                _cb.SpreadScale = ShiftGeometry.Spread(_mode, _cb.Distance, _cb.Radius);
                drawInformation.SetPixelShaderConstantBuffer(_cb);
            }
            catch (Exception exception)
            {
                SpectrumColorShiftTelemetry.Report(exception);
                throw;
            }
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            inputRect = ClampInputRect(inputRects[0]);
            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
            {
                outputRect = inputRect;
                outputOpaqueSubRect = default;
                return;
            }

            _cb.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            RefreshGeometry();
            UpdateConstants();

            outputRect = ShiftGeometry.Output(inputRect, _mode, _cb.Distance, _cb.Direction, _cb.Center, _cb.Radius);
            outputOpaqueSubRect = default;
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            if (inputRects.Length == 0)
                return;

            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
            {
                inputRects[0] = inputRect;
                return;
            }

            inputRects[0] = ShiftGeometry.Input(outputRect, _mode, _cb.Distance, _cb.Direction, _cb.Center, _cb.Radius);
        }

        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            => ShiftGeometry.Output(invalidInputRect, _mode, _cb.Distance, _cb.Direction, _cb.Center, _cb.Radius);

        private static float Sanitize(float value, float minimum, float maximum)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, minimum, maximum);

        private void RefreshGeometry()
        {
            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                return;

            _cb.Center = ShiftGeometry.Center(inputRect, _centerOffset);
            _cb.Radius = ShiftGeometry.Radius(inputRect, _cb.Center);
        }

        [InlineArray(Spectrum.MaxSteps)]
        private struct SampleTable
        {
            private Vector4 _element;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public Vector4 InputBounds;
            public Vector2 Direction;
            public Vector2 Center;
            public float Distance;
            public float Radius;
            public float Strength;
            public int Mode;
            public int Steps;
            public float SpreadScale;
            public float Pad1;
            public float Pad2;
            public SampleTable Samples;
        }
    }
}
