using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SpectrumColorShift;

internal sealed class SpectrumColorShiftCustomEffect : D2D1CustomShaderEffectBase
{
    private const int SourceInput = 0;
    private const int LinearInput = 1;

    private readonly LinearizeCustomEffect _linearized;

    public SpectrumColorShiftCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices))
    {
        _linearized = new LinearizeCustomEffect(devices);
        if (!IsEnabled)
            return;

        using var linearized = _linearized.Output;
        base.SetInput(LinearInput, linearized, true);
    }

    public new bool IsEnabled => base.IsEnabled && _linearized.IsEnabled;

    public new void SetInput(int index, ID2D1Image? input, RawBool invalidate)
    {
        base.SetInput(index, input, invalidate);
        if (index == SourceInput)
            _linearized.SetInput(SourceInput, input, invalidate);
    }

    protected override void DisposeCore(nint nativePointer, bool disposing)
    {
        if (disposing)
            _linearized.Dispose();

        base.DisposeCore(nativePointer, disposing);
    }

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
        Physical,
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
    public int Physical { set => SetValue((int)PropertyIndex.Physical, value); }

    [CustomEffect(2)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private PhysicalConstantBuffer _physicalCb;
        private ShiftMode _mode = ShiftMode.Linear;
        private DispersionLaw _dispersion = DispersionLaw.Grating;
        private Vector2 _centerOffset;
        private float _referenceWavelength = Spectrum.DefaultReferenceWavelength;
        private int _steps = Spectrum.DefaultSteps;
        private bool _tableStale = true;
        private bool _spectralStale = true;
        private bool _filterStale = true;
        private SpectralFilter.Layout _filter;
        private bool _physical;
        private Guid _shader = GUID_PixelShader;

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Distance)]
        public float Distance
        {
            get => _cb.Distance;
            set
            {
                _cb.Distance = Sanitize(value, -MaxInputPixel, MaxInputPixel);
                _filterStale = true;
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
                _filterStale = true;
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
                _filterStale = true;
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
                _spectralStale = true;
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
                _spectralStale = true;
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
                _spectralStale = true;
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Physical)]
        public int Physical
        {
            get => _physical ? 1 : 0;
            set
            {
                _physical = value != 0;
                UpdateConstants();
            }
        }

        internal int FilterTaps
        {
            get
            {
                if (!_physical)
                    return 0;

                RefreshPhysical();
                return _filter.Taps;
            }
        }

        public EffectImpl() : base(ShaderVariants.DefaultBytes)
        {
            _cb.Direction = Vector2.UnitX;
            _cb.Radius = 1f;
            _cb.Strength = 1f;
            _cb.Mode = (int)ShiftMode.Linear;
            _cb.Steps = Spectrum.DefaultSteps;
        }

        public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph)
        {
            base.Initialize(effectContext, transformGraph);
            ShaderVariants.LoadAll(effectContext);
        }

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                _cb.SpreadScale = ShiftGeometry.Spread(_mode, _cb.Distance, _cb.Radius);

                if (_physical)
                    RefreshPhysical();

                var shader = _physical && _filter.Taps > 0 ? ShaderVariants.Filter.Id(ShiftMode.Linear, _filter.Taps) : ShaderVariants.Stepped(_physical).Id(_mode, _steps);
                if (shader != _shader)
                {
                    drawInformation.SetPixelShader(shader, PixelOptions.None);
                    _shader = shader;
                }

                if (_physical)
                    SendPhysicalConstants();
                else
                    SendConstants();
            }
            catch (Exception exception)
            {
                SpectrumColorShiftTelemetry.Report(exception);
                throw;
            }
        }

        private void SendConstants()
        {
            if (_tableStale)
            {
                Spectrum.Fill(_cb.Samples, _steps, _referenceWavelength, _dispersion);
                _tableStale = false;
            }

            drawInformation!.SetPixelShaderConstantBuffer(_cb);
        }

        private void RefreshPhysical()
        {
            if (_spectralStale)
            {
                Spectrum.FillSpectral(_physicalCb.Samples, _steps, _referenceWavelength, _dispersion);
                _spectralStale = false;
                _filterStale = true;
            }

            if (_filterStale)
            {
                _filter = _mode == ShiftMode.Linear ? SpectralFilter.Fill(_physicalCb.Taps, _physicalCb.Samples, _steps, _cb.Distance, _cb.Direction) : default;
                _filterStale = false;
            }
        }

        private void SendPhysicalConstants()
        {
            _physicalCb.InputBounds = _cb.InputBounds;
            _physicalCb.Direction = _cb.Direction;
            _physicalCb.Center = _cb.Center;
            _physicalCb.Distance = _cb.Distance;
            _physicalCb.Radius = _cb.Radius;
            _physicalCb.Strength = _cb.Strength;
            _physicalCb.Mode = _cb.Mode;
            _physicalCb.Steps = Spectrum.Padded(_steps);
            _physicalCb.SpreadScale = _cb.SpreadScale;
            _physicalCb.FilterOrigin = _filter.Origin;
            _physicalCb.FilterVertical = _filter.Vertical ? 1 : 0;
            drawInformation!.SetPixelShaderConstantBuffer(_physicalCb);
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
            var requested = inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top
                ? inputRect
                : ShiftGeometry.Input(outputRect, _mode, _cb.Distance, _cb.Direction, _cb.Center, _cb.Radius);
            var used = _physical ? LinearInput : SourceInput;

            for (var index = 0; index < inputRects.Length; index++)
                inputRects[index] = index == used ? requested : default;
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

        [InlineArray(Spectrum.SpectralTableLength)]
        private struct SpectralSampleTable
        {
            private Vector4 _element;
        }

        [InlineArray(SpectralFilter.TableLength)]
        private struct FilterTapTable
        {
            private Vector4 _element;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysicalConstantBuffer
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
            public float FilterOrigin;
            public int FilterVertical;
            public SpectralSampleTable Samples;
            public FilterTapTable Taps;
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
