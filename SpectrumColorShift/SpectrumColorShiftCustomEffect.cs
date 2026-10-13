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
    private const int EffectInputs = LinearInput + 1;

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

    [CustomEffect(EffectInputs)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ShiftHeader _header;
        private ConstantBuffer _cb;
        private PhysicalConstantBuffer _physicalCb;
        private ShiftMode _mode = ShiftMode.Linear;
        private DispersionLaw _dispersion = DispersionLaw.Grating;
        private Vector2 _centerOffset;
        private float _referenceWavelength = Spectrum.DefaultReferenceWavelength;
        private int _steps = Spectrum.DefaultSteps;
        private bool _physical;
        private SpectrumKey? _tableKey;
        private SpectrumKey? _spectralKey;
        private FilterKey? _filterKey;
        private SpectralFilter.Layout _filter;
        private Guid _shader = GUID_PixelShader;

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Distance)]
        public float Distance
        {
            get => _header.Distance;
            set
            {
                _header.Distance = Sanitize(value, -MaxInputPixel, MaxInputPixel);
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
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Angle)]
        public float Angle
        {
            get => MathF.Atan2(_header.Direction.Y, _header.Direction.X) * 180f / MathF.PI;
            set
            {
                var radians = float.IsFinite(value) ? value * MathF.PI / 180f : 0f;
                _header.Direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
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
            get => _header.Strength;
            set
            {
                _header.Strength = Sanitize(value, 0f, 1f);
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
                _header.Steps = _steps;
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

        internal int FilterTaps => _physical ? CurrentFilter().Taps : 0;

        private SpectrumKey CurrentSpectrum => new(_steps, _referenceWavelength, _dispersion);

        public EffectImpl() : base(ShaderVariants.DefaultBytes)
        {
            _header.Direction = Vector2.UnitX;
            _header.Radius = 1f;
            _header.Strength = 1f;
            _header.Steps = Spectrum.DefaultSteps;
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
                var spread = ShiftGeometry.Spread(_mode, _header.Distance, _header.Radius);
                var filter = _physical ? CurrentFilter() : default;

                SelectShader(filter);
                if (_physical)
                    SendPhysicalConstants(spread, filter);
                else
                    SendConstants(spread);
            }
            catch (Exception exception)
            {
                SpectrumColorShiftTelemetry.Report(exception);
                throw;
            }
        }

        private SpectralFilter.Layout CurrentFilter()
        {
            var spectral = CurrentSpectrum;
            if (_spectralKey != spectral)
            {
                Spectrum.FillSpectral(_physicalCb.Samples, spectral.Steps, spectral.Reference, spectral.Law);
                _spectralKey = spectral;
            }

            var key = new FilterKey(spectral, _header.Distance, _header.Direction, _mode);
            if (_filterKey != key)
            {
                _filter = _mode == ShiftMode.Linear ? SpectralFilter.Fill(_physicalCb.Taps, _physicalCb.Samples, _steps, _header.Distance, _header.Direction) : default;
                _filterKey = key;
            }

            return _filter;
        }

        private void SelectShader(SpectralFilter.Layout filter)
        {
            var shader = filter.Taps > 0
                ? ShaderVariants.Filter.Id(ShiftMode.Linear, filter.Taps)
                : ShaderVariants.Stepped(_physical).Id(_mode, _steps);
            if (shader == _shader)
                return;

            drawInformation!.SetPixelShader(shader, PixelOptions.None);
            _shader = shader;
        }

        private void SendConstants(float spread)
        {
            var spectral = CurrentSpectrum;
            if (_tableKey != spectral)
            {
                Spectrum.Fill(_cb.Samples, spectral.Steps, spectral.Reference, spectral.Law);
                _tableKey = spectral;
            }

            _cb.Header = _header;
            _cb.SpreadScale = spread;
            drawInformation!.SetPixelShaderConstantBuffer(_cb);
        }

        private void SendPhysicalConstants(float spread, SpectralFilter.Layout filter)
        {
            _physicalCb.Header = _header;
            _physicalCb.SpreadScale = spread;
            _physicalCb.FilterOrigin = filter.Origin;
            _physicalCb.FilterVertical = filter.Vertical ? 1 : 0;
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

            _header.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            RefreshGeometry();
            UpdateConstants();

            outputRect = ShiftGeometry.Output(inputRect, _mode, _header.Distance, _header.Direction, _header.Center, _header.Radius);
            outputOpaqueSubRect = default;
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            var requested = inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top
                ? inputRect
                : ShiftGeometry.Input(outputRect, _mode, _header.Distance, _header.Direction, _header.Center, _header.Radius);
            var activeInput = _physical ? LinearInput : SourceInput;

            for (var index = 0; index < inputRects.Length; index++)
                inputRects[index] = index == activeInput ? requested : default;
        }

        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            => ShiftGeometry.Output(invalidInputRect, _mode, _header.Distance, _header.Direction, _header.Center, _header.Radius);

        private static float Sanitize(float value, float minimum, float maximum)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, minimum, maximum);

        private void RefreshGeometry()
        {
            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                return;

            _header.Center = ShiftGeometry.Center(inputRect, _centerOffset);
            _header.Radius = ShiftGeometry.Radius(inputRect, _header.Center);
        }

        private readonly record struct SpectrumKey(int Steps, float Reference, DispersionLaw Law);

        private readonly record struct FilterKey(SpectrumKey Spectral, float Distance, Vector2 Direction, ShiftMode Mode);

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
        private struct ShiftHeader
        {
            public Vector4 InputBounds;
            public Vector2 Direction;
            public Vector2 Center;
            public float Distance;
            public float Radius;
            public float Strength;
            public int Steps;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public ShiftHeader Header;
            public float SpreadScale;
            public float Pad1;
            public float Pad2;
            public float Pad3;
            public SampleTable Samples;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysicalConstantBuffer
        {
            public ShiftHeader Header;
            public float SpreadScale;
            public float FilterOrigin;
            public int FilterVertical;
            public float Pad;
            public SpectralSampleTable Samples;
            public FilterTapTable Taps;
        }
    }
}
