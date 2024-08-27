using Godot;
using Godot.Collections;
using System;
/*
 *	TPCSharp, a C# translation of Third Person Camera.
 *	Copyright (C) 2024 Framebuffer, original by JeanKouss.
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

/// <summary>
/// Main module of TPCSharp.
/// </summary>
[GlobalClass]
public partial class TPCSharpCamera : Node3D
{
	public override void _Ready()
	{
		// Load from the node tree the camera, RotationPivot, OffsetPivot, SpringArm, CameraMarker, CameraShaker
		CallDeferred(MethodName.LoadNodes);

		// Map variables to fields.
		CallDeferred(MethodName.MapVariables);
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateCameraProperties();
		if (Engine.IsEditorHint())
		{
			Vector3 cameraMarkerGlobalPosition =
					(new Vector3(x: 0.0f, y: 0.0f, z: 0.1f)
					.Rotated(axis: new Vector3(x: 1.0f, y: 0.0f, z: 0.0f), angle: (float)Mathf.DegToRad(initialDiveAngleDeg))
					.Rotated(axis: new Vector3(x: 0.0f, y: 1.0f, z: 0.0f), angle: (float)Mathf.DegToRad(-cameraHorizontalRotationDeg))
					* cameraSpringArm.SpringLength) + cameraSpringArm.GlobalPosition;

			CameraMarker.GlobalPosition = cameraMarkerGlobalPosition;
		}

		TweenCameraToMarker();

		// original line: _camera_offset_pivot.global_position = _camera_offset_pivot.get_parent().to_global(Vector3(pivot_offset.x, pivot_offset.y, 0.0))
		// translation change: get the parent of OffsetPivot, cast to Node3D, transform local point to global and apply to OffsetPivot.GlobalPosition.
		Node3D offsetParent = (Node3D)offsetPivot.GetParent();
		offsetPivot.GlobalPosition = offsetParent.ToGlobal(new Vector3(pivotOffset.X, pivotOffset.Y, 0.0f));

		// because of limitations with Godot's API, a variable is declared before assigning it a value.
		Vector3 globalRotationDeg = rotationPivot.GlobalRotationDegrees;
		globalRotationDeg.X = (float)initialDiveAngleDeg;

		// don't know where the original globalPosition comes from, but I assume it's from the only globalPosition variable in this method?
		rotationPivot.GlobalPosition = offsetParent.GlobalPosition;

		ProcessTiltInput();
		ProcessHorizontalRotationInput();
		UpdateCameraTilt();
		UpdateCameraHorizontalRotation();

	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (MouseFollow && @event is InputEventMouseMotion)
		{
			var e = @event as InputEventMouseMotion;
			cameraHorizontalRotationDeg += e.Relative.X * 0.1 * mouseSensitivenessX;
			cameraTiltDeg -= e.Relative.Y * 0.7 * mouseSensitivenessY;
			return;
		}
	}
}

// Node references.
// TPC reads from the Scene's node tree and loads a reference to its nodes on _Ready();
public partial class TPCSharpCamera
{
    private Camera3D activeCamera;
    private Node3D rotationPivot;
    private Node3D offsetPivot;
    private SpringArm3D cameraSpringArm;
    private Marker3D cameraMarker;
    private MeshInstance3D pivotDebug;
    private TPCShaker cameraShaker;

    // scene elements
    /// <summary>
    /// Output camera to the Viewport.
    /// </summary>
    Camera3D ActiveCamera { get => activeCamera; set => activeCamera = value; }

    /// <summary>
    /// Determines global position of camera?
    /// </summary>
    Node3D RotationPivot { get => rotationPivot; set => rotationPivot = value; }

    /// <summary>
    /// Point to follow with <see cref="activeCamera"/>.
    /// </summary>
    Node3D OffsetPivot { get => offsetPivot; set => offsetPivot = value; }

    /// <summary>
    /// Creates a <see cref="SpringArm3D"/> between the <see cref="CameraMarker"/> and the <see cref="OffsetPivot"/>. Any physics interaction comes from this object.
    /// </summary>
    SpringArm3D CameraSpringArm { get => cameraSpringArm; set => cameraSpringArm = value; }

    /// <summary>
    /// Origin point of <see cref="activeCamera"/>
    /// </summary>
    Marker3D CameraMarker { get => cameraMarker; set => cameraMarker = value; }

    /// <summary>
    /// If enabled, renders a sphere on top of the <see cref="CameraMarker"/>.
    /// </summary>
    MeshInstance3D PivotDebug { get => pivotDebug; set => pivotDebug = value; }

    /// <summary>
    /// Allows to add shaking to <see cref="activeCamera"/>'s HOffset and VOffset.
    /// </summary>
    TPCShaker CameraShaker { get => cameraShaker; set => cameraShaker = value; }
}

// Custom properties
// ------------------------------------
// TPC
// ------------------------------------
public partial class TPCSharpCamera
{
    private double initialDiveAngleDeg = -45.0;
    private double distanceFromPivot = 10.0;
    private Vector2 pivotOffset = new(0, 0);
    private double tiltUpperLimitDeg = 60.0;
    private double tiltLowerLimitDeg = -60.0;
    private double verticalTiltSensitiveness = 10.0;
    private double horizontalRotationSensitiveness = 10.0;
    private double cameraSpeed = 0.1;
    private double cameraHorizontalRotationDeg = -45;

    /// <summary>
    /// Sets the <see cref="SpringArm3D.SpringLength"/> from <see cref="CameraSpringArm"/>. On scene, it's the distance between <see cref="activeCamera"/> and <see cref="RotationPivot"/>.
    /// </summary>
    [Export]
    public double DistanceFromPivot { get => distanceFromPivot; set => distanceFromPivot = value; }

    /// <summary>
    /// Distance from <see cref="RotationPivot"/>.
    /// </summary>
    [Export]
    public Vector2 PivotOffset { get => pivotOffset; set => pivotOffset = value; }

    /// <summary>
    /// The angle between <see cref="activeCamera"/> and <see cref="RotationPivot"/> when it first enters the <see cref="SceneTree"/>. It goes from -90� to 90� degrees and is clamped between <see cref="TiltLowerLimitDeg"/> and <see cref="TiltUpperLimitDeg"/>. 
    /// </summary>
    [Export(PropertyHint.Range, "-90.0,90.0")]
	public double InitialDiveAngleDeg
	{
		get => initialDiveAngleDeg;
		set => initialDiveAngleDeg = Mathf.Clamp(value, TiltLowerLimitDeg, TiltUpperLimitDeg);
	}
	
    /// <summary>
    /// Maximum angle on which <see cref="activeCamera"/> will tilt in relation to <see cref="RotationPivot"/>. When <see cref="SpringArm3D"/> hits any object, the camera will not tilt past this angle.
    /// </summary>
    [Export(PropertyHint.Range, "-90.0,90.0")]
    public double TiltUpperLimitDeg { get => tiltUpperLimitDeg; set => tiltUpperLimitDeg = value; }

    /// <summary>
    /// Minimum angle on which <see cref="activeCamera"/> will tilt in relation to <see cref="RotationPivot"/>. When <see cref="SpringArm3D"/> hits any object, the camera will not tilt past this angle.
    /// </summary>
    [Export(PropertyHint.Range, "-90.0,90.0")]
    public double TiltLowerLimitDeg { get => tiltLowerLimitDeg; set => tiltLowerLimitDeg = value; }

    /// <summary>
    /// Speed at which <see cref="activeCamera"/> will change <b>vertical</b> angle. It's the speed at which the interpolation between angles will occur.
    /// </summary>
    [Export(PropertyHint.Range, "1.0,1000.0")]
    public double VerticalTiltSensitiveness { get => verticalTiltSensitiveness; set => verticalTiltSensitiveness = value; }

    /// <summary>
    /// Speed at which <see cref="activeCamera"/> will <b>rotate around</b> <see cref="RotationPivot"/>. It's the speed at which the interpolation between angles will occur.
    /// </summary>
    [Export(PropertyHint.Range, "1.0,1000.0")]
    public double HorizontalRotationSensitiveness { get => horizontalRotationSensitiveness; set => horizontalRotationSensitiveness = value; }

    /// <summary>
    /// Speed at which <see cref="activeCamera"/> will traslate between points. This will speed up or slow down any rotation and 
    /// </summary>
    [Export(PropertyHint.Range, "0.1,1")]
    public double CameraSpeed { get => cameraSpeed; set => cameraSpeed = value; }

    /// <summary>
    /// <see cref="activeCamera"/> horizontal rotation angle, independent of the calculated horizontal rotation angle.
    /// </summary>
    [Export]
    public double CameraHorizontalRotationDeg { get => cameraHorizontalRotationDeg; set => cameraHorizontalRotationDeg = value; }
}

// ------------------------------------
// Mouse
// ------------------------------------
public partial class TPCSharpCamera
{
    private double mouseSensitivenessY = 1.0f;
    private bool mouseFollow = false;
    private double mouseSensitivenessX = 1.0f;

    /// <summary>
    /// Enables mouse controls.
    /// </summary>
    [ExportGroup("Mouse")]
    [Export]
    public bool MouseFollow { get => mouseFollow; set => mouseFollow = value; }
    
	/// <summary>
    /// Speed on which mouse input is processed on the X axis. A higher value will amplify the effect on the camera's position.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,100.0")]
    public double MouseSensitivenessX { get => mouseSensitivenessX; set => mouseSensitivenessX = value; }
    
	/// <summary>
    /// Speed on which mouse input is processed on the Y axis. A higher value will amplify the effect on the camera's position.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,100.0")]
    public double MouseSensitivenessY { get => mouseSensitivenessY; set => mouseSensitivenessY = value; }
}

// ------------------------------------
// CameraShake
// ------------------------------------
public partial class TPCSharpCamera
{
    private Array<TPCShakePreset> shakePresets;

    /// <summary>
    /// Current CameraShake presets loaded into CameraShaker
    /// </summary>
    [ExportGroup("Camera Shake")]
    [Export]
    public Array<TPCShakePreset> ShakePresets { get => shakePresets; set => shakePresets = value; } // cannot auto init
}

// ------------------------------------
// SpringArm3D
// ------------------------------------
public partial class TPCSharpCamera
{
    private double springArmMargin = 0.01f;
    private int springArmCollissionMask = 1;

    /// <summary>
    /// Mask value belonging to <see cref="CameraSpringArm"/>. It will react to members with the same mask number.
    /// </summary>
    // Mapping Camera3D Properties
    [ExportCategory("SpringArm3D")]
    [Export]
    public int SpringArmCollissionMask { get => springArmCollissionMask; set => springArmCollissionMask = value; }

    /// <summary>
    /// Property matching <see cref="SpringArm3D.Margin"/>.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 100.0, 0.01, or_greater, or_less, hide_slider, suffix:m")]
    public double SpringArmMargin { get => springArmMargin; set => springArmMargin = value; }
}

// ------------------------------------
// activeCamera properties
// ------------------------------------
public partial class TPCSharpCamera
{
    private double fov = 75.0;
    private bool current = false;
    private Camera3D.ProjectionType projectionType = Camera3D.ProjectionType.Perspective;
    private double near = 0.05;
    private double far = 4000.0;
    private Camera3D.KeepAspectEnum keepAspect = Camera3D.KeepAspectEnum.Height;
    private Camera3D.DopplerTrackingEnum dopplerTracking = Camera3D.DopplerTrackingEnum.Disabled;
    private double cameraTiltDeg = 0;
    private uint cullMask = 1048575;

    /// <summary>
    /// Mapped property of <see cref="Camera3D.Projection"/>
    /// </summary>
    [Export]
    public Camera3D.ProjectionType ProjectionType { get => projectionType; set => projectionType = value; }
    /// <summary>
    /// Mapped propery of <see cref="Camera3D.Current"/>
    /// </summary>
    [Export]
    public bool Current { get => current; set => current = value; } // exposes the Current from Camera3D, mirror the value from Camera. Set on _Ready();

    /// <summary>
    /// Mapped property from <see cref="Camera3D.Fov"/>
    /// </summary>
    [Export(PropertyHint.Range, "1.0, 179.0, 0.1, suffix:�")]
    public double Fov { get => fov; set => fov = value; }
    
	/// <summary>
    /// Mapped property from <see cref="Camera3D.Near"/>
    /// </summary>
    [Export]
    public double Near { get => near; set => near = value; }
    
	/// <summary>
    /// Mapped property from <see cref="Camera3D.Far"/>
    /// </summary>
    [Export]
    public double Far { get => far; set => far = value; }
    
	/// <summary>
    /// Mapped property from <see cref="Camera3D.KeepAspect"/>
    /// </summary>
    [ExportCategory("Camera3D")]
    [Export]
    public Camera3D.KeepAspectEnum KeepAspect { get => keepAspect; set => keepAspect = value; }
    
	/// <summary>
    /// Mapped property from <see cref="Camera3D.DopplerTracking"/>
    /// </summary>
    [Export]
    public Camera3D.DopplerTrackingEnum DopplerTracking { get => dopplerTracking; set => dopplerTracking = value; }

    /// <summary>
    /// <see cref="activeCamera"/> tilt degree.
    /// </summary>
    [Export(PropertyHint.Range, "1.0, 179.0, 0.1, suffix:�")]
    public double CameraTiltDeg { get => cameraTiltDeg; set => cameraTiltDeg = value; }

    /// <summary>
    /// Mapped property of <see cref="Camera3D.CullMask"/>
    /// </summary>
    [Export(PropertyHint.Layers3DRender)]
    public uint CullMask { get => cullMask; set => cullMask = value; }
}

// ------------------------------------
// Parent Scene properties
// ------------------------------------
public partial class TPCSharpCamera
{
    private Godot.Environment environment;
    private CameraAttributes attributes;

    /// <summary>
    /// <see cref="Godot.Environment"/> loaded into <see cref="activeCamera"/> to render with.
    /// </summary>
    [Export]
    public Godot.Environment Environment { get => environment; set => environment = value; }

    /// <summary>
    /// Mapped property of <see cref="Camera3D.Attributes"/>.
    /// </summary>
    [Export]
    public CameraAttributes Attributes { get => attributes; set => attributes = value; }
}

// ------------------------------------
// Custom methods
// ------------------------------------
public partial class TPCSharpCamera
{
    /// <summary>
    /// Retrieves active <see cref="Camera3D"/>.
    /// </summary>
    /// <returns></returns>
    public Camera3D GetCamera() => activeCamera;

	/// <summary>
	/// Gets vector pointing in front of the <see cref="OffsetPivot"/>.
	/// </summary>
	/// <returns></returns>
	public Vector3 GetFrontDirection()
	{
		Vector3 direction = OffsetPivot.GlobalPosition - activeCamera.GlobalPosition;
		direction.Y = 0.0f;
		direction = direction.Normalized();
		return direction;
	}

	/// <summary>
	/// Gets vector pointing back of the <see cref="OffsetPivot"/>.
	/// </summary>
	/// <returns></returns>
	public Vector3 GetBackDirection() => -GetFrontDirection();

	/// <summary>
	/// Gets vector pointing to the left of the <see cref="OffsetPivot"/>.
	/// </summary>
	/// <returns></returns>
	public Vector3 GetLeftDirection() => GetFrontDirection().Rotated(Vector3.Up, (float)Math.PI / 2);

	/// <summary>
	/// Gets vector pointing to the right of the <see cref="OffsetPivot"/>.
	/// </summary>
	/// <returns></returns>
	public Vector3 GetRightDirection() => GetFrontDirection().Rotated(Vector3.Up, -(float)Math.PI / 2);


	/// <summary>
	/// Load nodes from the tree by string path
	/// </summary>
	private void LoadNodes()
	{
		try
		{
			activeCamera = GetNode<Camera3D>("Camera");
			rotationPivot = GetNode<Node3D>("RotationPivot");
			offsetPivot = GetNode<Node3D>("RotationPivot/OffsetPivot");
			cameraSpringArm = GetNode<SpringArm3D>("RotationPivot/OffsetPivot/CameraSpringArm");
			cameraMarker = GetNode<Marker3D>("RotationPivot/OffsetPivot/CameraSpringArm/CameraMarker");
			pivotDebug = GetNode<MeshInstance3D>("RotationPivot/OffsetPivot/PivotDebug");
			cameraShaker = GetNode<TPCShaker>("CameraShaker");
			activeCamera.TopLevel = true;
            activeCamera.Current = true;
		}
		catch (Exception e)
		{
			GD.PrintErr(e.Message);
		}

	}

	/// <summary>
	/// Gets the property as Variant, casts and assigns the respective property to the object it belongs to.
	/// </summary>
	private void MapVariables()
	{
        //// Camera3D properties
        //Variant currentCamera = activeCamera.Get(Camera3D.PropertyName.Current);
        //current = currentCamera.AsBool();

        //Variant aspect = activeCamera.Get(Camera3D.PropertyName.KeepAspect);
        //keepAspect = Enum.Parse<Camera3D.KeepAspectEnum>(aspect.AsString());

        //Variant cullMask = activeCamera.Get(Camera3D.PropertyName.CullMask);
        //      cullMask = cullMask.AsUInt32();

        //Variant doppler = activeCamera.Get(Camera3D.PropertyName.DopplerTracking);
        //dopplerTracking = Enum.Parse<Camera3D.DopplerTrackingEnum>(doppler.AsString());

        //Variant projectionType = activeCamera.Get(Camera3D.PropertyName.Projection);
        //projectionType = Enum.Parse<Camera3D.ProjectionType>(projectionType.ToString());

        //Variant fov = activeCamera.Get(Camera3D.PropertyName.Fov);
        //fov = fov.AsDouble();

        //Variant near = activeCamera.Get(Camera3D.PropertyName.Near);
        //near = near.AsDouble();

        //Variant far = activeCamera.Get(Camera3D.PropertyName.Far);
        //far = far.AsDouble();

        //// SpringArm3D properties
        //Variant collisionMask = CameraSpringArm.Get(SpringArm3D.PropertyName.CollisionMask);
        //SpringArmCollissionMask = collisionMask.AsInt32();

        //Variant margin = CameraSpringArm.Get(SpringArm3D.PropertyName.Margin);
        //SpringArmMargin = margin.AsDouble();

        //Variant distanceFromPivot = CameraSpringArm.Get(SpringArm3D.PropertyName.SpringLength);
        //DistanceFromPivot = distanceFromPivot.AsDouble();

        // Camera3D properties
        current = activeCamera.Get(Camera3D.PropertyName.Current).AsBool();

        keepAspect = Enum.Parse<Camera3D.KeepAspectEnum>(activeCamera.Get(Camera3D.PropertyName.KeepAspect).AsString());

        cullMask = activeCamera.Get(Camera3D.PropertyName.CullMask).AsUInt32();

        dopplerTracking = Enum.Parse<Camera3D.DopplerTrackingEnum>(activeCamera.Get(Camera3D.PropertyName.DopplerTracking).AsString());

        projectionType = Enum.Parse<Camera3D.ProjectionType>(activeCamera.Get(Camera3D.PropertyName.Projection).ToString());

        fov = activeCamera.Get(Camera3D.PropertyName.Fov).AsDouble();

        near = activeCamera.Get(Camera3D.PropertyName.Near).AsDouble();

        far = activeCamera.Get(Camera3D.PropertyName.Far).AsDouble();

        // SpringArm3D properties
        springArmCollissionMask = CameraSpringArm.Get(SpringArm3D.PropertyName.CollisionMask).AsInt32();

        springArmMargin = CameraSpringArm.Get(SpringArm3D.PropertyName.Margin).AsDouble();

        distanceFromPivot = CameraSpringArm.Get(SpringArm3D.PropertyName.SpringLength).AsDouble();
    }

	/// <summary>
	/// Calculates linear interpolation values for each axis, and updates the <see cref="activeCamera"/> global position. GDScript can do this in one line. On C#, each lerp for each axis must be listed on its own line.
	/// </summary>
	private void TweenCameraToMarker()
	{
		float lerpX = (float)Mathf.Lerp(activeCamera.GlobalPosition.X, cameraMarker.GlobalPosition.X, CameraSpeed);
		float lerpY = (float)Mathf.Lerp(activeCamera.GlobalPosition.Y, cameraMarker.GlobalPosition.Y, CameraSpeed);
		float lerpZ = (float)Mathf.Lerp(activeCamera.GlobalPosition.Z, cameraMarker.GlobalPosition.Z, CameraSpeed);

		activeCamera.GlobalPosition = new Vector3(x: lerpX, y: lerpY, z: lerpZ);
	}

	/// <summary>
	/// Writes the values inside TPC's internal fields to <see cref="activeCamera"/>. Usually executed at the start of every _Physics(); loop.
	/// </summary>
	private void UpdateCameraProperties()
	{
		activeCamera.KeepAspect = KeepAspect;
		activeCamera.CullMask = CullMask;
		activeCamera.DopplerTracking = DopplerTracking;
		activeCamera.Projection = ProjectionType;
		activeCamera.Fov = (float)Fov;
		activeCamera.Near = (float)Near;
		activeCamera.Far = (float)Far;
		if (activeCamera.Environment != Environment)
		{
			activeCamera.Environment = Environment;
		}

		if (activeCamera.Attributes != Attributes)
		{
			activeCamera.Attributes = Attributes;
		}
	}

	private void ProcessHorizontalRotationInput()
	{
		if (InputMap.HasAction("tp_camera_right") && InputMap.HasAction("tp_camera_left"))
		{
			float cameraHorizontalRotationVariation = Input.GetActionStrength("tp_camera_right") - Input.GetActionStrength("tp_camera_left");
			cameraHorizontalRotationVariation = (float)cameraHorizontalRotationVariation * (float)GetProcessDeltaTime() * 30 * (float)horizontalRotationSensitiveness;
			cameraHorizontalRotationDeg =+ cameraHorizontalRotationVariation;
		}
	}

	private void ProcessTiltInput()
	{
		if (InputMap.HasAction("tp_camera_up") && InputMap.HasAction("tp_camera_down"))
		{
			float tiltVariation = Input.GetActionStrength("tp_camera_up") - Input.GetActionStrength("tp_camera_down");
			tiltVariation = tiltVariation * (float)GetProcessDeltaTime() * 5 * (float)verticalTiltSensitiveness;
			cameraTiltDeg = Math.Clamp(cameraTiltDeg + tiltVariation, tiltLowerLimitDeg - initialDiveAngleDeg, tiltUpperLimitDeg - initialDiveAngleDeg);
		}
	}

	private void UpdateCameraTilt()
	{
		double tiltFinalVal = Mathf.Clamp(initialDiveAngleDeg + cameraTiltDeg, tiltLowerLimitDeg, tiltUpperLimitDeg);
		Tween tween = CreateTween();
		tween.TweenProperty(activeCamera, "global_rotation_degrees:x", tiltFinalVal, 0.1);
	}

	private void UpdateCameraHorizontalRotation()
	{
		var tween = CreateTween();
		tween.TweenProperty(rotationPivot, "global_rotation_degrees:y", cameraHorizontalRotationDeg * -1, 0.1).AsRelative();
		cameraHorizontalRotationDeg = 0.0; // reset value

		Vector2 vectToOffsetPivot = (new Vector2(offsetPivot.GlobalPosition.X, offsetPivot.GlobalPosition.Z) - new Vector2(activeCamera.GlobalPosition.X, activeCamera.GlobalPosition.Z)).Normalized();

		var activeCameraGlobalRotation = activeCamera.GlobalRotation;
		activeCameraGlobalRotation.Y = -new Vector2(0.0f, -1.0f).AngleTo(vectToOffsetPivot.Normalized());
	}

	public void ApplyPresetShake(int presetNumber)
	{
		cameraShaker.ApplyPresetShake(shakePresets[presetNumber]);
	}
}
