using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 *	CameraShake translation.
 *	Copyright (C) 2024 Framebuffer, original by JeanKouss.
 *	
 *	Original file includes the following acknowledgements:
 *	    Credit to: https://shaggydev.com/2022/02/23/screen-shake-godot/
 *      Also see: https://kidscancode.org/godot_recipes/3.x/2d/screen_shake/index.html
 *	
 *	Released under the MIT Licence.
 *	
 *	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 *  
 *  Based on version 1.3.0, built for Godot 4.x
 *  
 *  Note: for readability, the class is split into chunks.
 */

public partial class TPCShaker : Node3D
{
    [Export]
    public Camera3D ActiveCamera { get; set; }
    private TPCShakePreset ShakerPreset { get; set; }
    private RandomNumberGenerator Rand { get; set; } = new RandomNumberGenerator();
    private FastNoiseLite Noise { get; set; } = new FastNoiseLite();
    private float CurrentShakeStrength { get; set; } = 0.0f;
    private float NoiseI { get; set; } = 0.0f;
    
    public override void _Ready()
    {
        Rand.Randomize();

        // Set the noise type.
        Noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

        // Randomise generated noise.
        Noise.Seed = (int)Rand.Randi();

        // Frequency affects how quickly the noise changes values.
        // The higher the frequency is, the faster it changes.
        Noise.Frequency = 1.0f / 2.0f;
    }

    public void ChangePreset(TPCShakePreset newPreset) => ShakerPreset = newPreset;

    public void ApplyNoiseShake() => CurrentShakeStrength = ShakerPreset.Strength;

    public void ApplyPresetShake(TPCShakePreset preset)
    {
        ShakerPreset = preset;
        CurrentShakeStrength = preset.Strength;
    }

    private Vector2 GetNoiseOffset(float delta)
    {
        NoiseI += delta * ShakerPreset.Volatility;

        // Set the X values of each call to GetNoise2D() to a different value.
        // so that our X and Y vectors will be reading from unrelated areas of noise.
        return new Vector2(
            Noise.GetNoise2D(1, NoiseI) * CurrentShakeStrength,
            Noise.GetNoise2D(100, NoiseI) * CurrentShakeStrength
            );
    }

    public override void _Process(double delta)
    {
        if (ShakerPreset == null) return;

        // Fade intensity over time.
        CurrentShakeStrength = (float)Mathf.Lerp(CurrentShakeStrength, 0.0f, ShakerPreset.DecayRate * delta);
        Vector2 noiseOffset = GetNoiseOffset((float)delta);
        ActiveCamera.HOffset = noiseOffset.X;
        ActiveCamera.VOffset = noiseOffset.Y;
    }
}

/// <summary>
/// Volatility, strength and decay rate values for the Camera Shaker.
/// </summary>
public partial class TPCShakePreset : Resource
{
    /// <summary>
    /// How quickly to move through the noise.
    /// </summary>
    [Export]
    public float Volatility { get; set; } = 0.0f;

    /// <summary>
    /// How much to multiply the Noise value by. Noise returns values between (-1, 1).
    /// </summary>
    [Export]
    public float Strength { get; set; } = 0.0f;

    /// <summary>
    /// Multiplier for lerping the shake strength to zero.
    /// </summary>
    [Export]
    public float DecayRate { get; set; } = 0.0f;
}

