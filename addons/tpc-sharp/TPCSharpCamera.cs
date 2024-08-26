#if TOOLS
using Godot;
using Godot.Collections;
using System;
using static Godot.Camera3D;

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
[Tool]
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
					.Rotated(axis: new Vector3(x: 1.0f, y: 0.0f, z: 0.0f), angle: (float)Mathf.DegToRad(_initialDiveAngleDeg))
					.Rotated(axis: new Vector3(x: 0.0f, y: 1.0f, z: 0.0f), angle: (float)Mathf.DegToRad(-CameraHorizontalRotationDeg))
					* CameraSpringArm.SpringLength) + CameraSpringArm.GlobalPosition;

			CameraMarker.GlobalPosition = cameraMarkerGlobalPosition;
		}

		TweenCameraToMarker();

		// original line: _camera_offset_pivot.global_position = _camera_offset_pivot.get_parent().to_global(Vector3(pivot_offset.x, pivot_offset.y, 0.0))
		// translation change: get the parent of OffsetPivot, cast to Node3D, transform local point to global and apply to OffsetPivot.GlobalPosition.
		Node3D offsetParent = (Node3D)OffsetPivot.GetParent();
		OffsetPivot.GlobalPosition = offsetParent.ToGlobal(new Vector3(PivotOffset.X, PivotOffset.Y, 0.0f));

		// because of limitations with Godot's API, a variable is declared before assigning it a value.
		Vector3 globalRotationDeg = RotationPivot.GlobalRotationDegrees;
		globalRotationDeg.X = (float)InitialDiveAngleDeg;

		// don't know where the original globalPosition comes from, but I assume it's from the only globalPosition variable in this method?
		RotationPivot.GlobalPosition = offsetParent.GlobalPosition;

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
			CameraHorizontalRotationDeg += e.Relative.X * 0.1 * MouseSensitivenessX;
			CameraTiltDeg -= e.Relative.Y * 0.7 * MouseSensitivenessY;
			return;
		}
	}
}

// Node references.
// TPC reads from the Scene's node tree and loads a reference to its nodes on _Ready();
public partial class TPCSharpCamera
{
	// scene elements
	/// <summary>
	/// Output camera to the Viewport.
	/// </summary>
	Camera3D ActiveCamera { get; set; }

	/// <summary>
	/// Determines global position of camera?
	/// </summary>
	Node3D RotationPivot { get; set; }

	/// <summary>
	/// Point to follow with <see cref="ActiveCamera"/>.
	/// </summary>
	Node3D OffsetPivot { get; set; }

	/// <summary>
	/// Creates a <see cref="SpringArm3D"/> between the <see cref="CameraMarker"/> and the <see cref="OffsetPivot"/>. Any physics interaction comes from this object.
	/// </summary>
	SpringArm3D CameraSpringArm { get; set; }

	/// <summary>
	/// Origin point of <see cref="ActiveCamera"/>
	/// </summary>
	Marker3D CameraMarker { get; set; }

	/// <summary>
	/// If enabled, renders a sphere on top of the <see cref="CameraMarker"/>.
	/// </summary>
	MeshInstance3D PivotDebug { get; set; }

	/// <summary>
	/// Allows to add shaking to <see cref="ActiveCamera"/>'s HOffset and VOffset.
	/// </summary>
	TPCShaker CameraShaker { get; set; }
}

// Custom properties
// ------------------------------------
// TPC
// ------------------------------------
public partial class TPCSharpCamera
{
	/// <summary>
	/// Sets the <see cref="SpringArm3D.SpringLength"/> from <see cref="CameraSpringArm"/>. On scene, it's the distance between <see cref="ActiveCamera"/> and <see cref="RotationPivot"/>.
	/// </summary>
	[Export]
	public double DistanceFromPivot { get; set; } = 10.0; // mapped

	/// <summary>
	/// Distance from <see cref="RotationPivot"/>.
	/// </summary>
	[Export]
	public Vector2 PivotOffset { get; set; } = new Vector2(0, 0);

	/// <summary>
	/// The angle between <see cref="ActiveCamera"/> and <see cref="RotationPivot"/> when it first enters the <see cref="SceneTree"/>. It goes from -90� to 90� degrees and is clamped between <see cref="TiltLowerLimitDeg"/> and <see cref="TiltUpperLimitDeg"/>. 
	/// </summary>
	[Export(PropertyHint.Range, "-90.0,90.0")]
	public double InitialDiveAngleDeg
	{
		get => _initialDiveAngleDeg;
		set => _initialDiveAngleDeg = Mathf.Clamp(value, TiltLowerLimitDeg, TiltUpperLimitDeg);
	}
	private double _initialDiveAngleDeg = -45.0;

	/// <summary>
	/// Maximum angle on which <see cref="ActiveCamera"/> will tilt in relation to <see cref="RotationPivot"/>. When <see cref="SpringArm3D"/> hits any object, the camera will not tilt past this angle.
	/// </summary>
	[Export(PropertyHint.Range, "-90.0,90.0")]
	public double TiltUpperLimitDeg { get; set; } = 60.0;

	/// <summary>
	/// Minimum angle on which <see cref="ActiveCamera"/> will tilt in relation to <see cref="RotationPivot"/>. When <see cref="SpringArm3D"/> hits any object, the camera will not tilt past this angle.
	/// </summary>
	[Export(PropertyHint.Range, "-90.0,90.0")]
	public double TiltLowerLimitDeg { get; set; } = -60.0;

	/// <summary>
	/// Speed at which <see cref="ActiveCamera"/> will change <b>vertical</b> angle. It's the speed at which the interpolation between angles will occur.
	/// </summary>
	[Export(PropertyHint.Range, "1.0,1000.0")]
	public double VerticalTiltSensitiveness { get; set; } = 10.0; // original name = TiltSensitiveness.

	/// <summary>
	/// Speed at which <see cref="ActiveCamera"/> will <b>rotate around</b> <see cref="RotationPivot"/>. It's the speed at which the interpolation between angles will occur.
	/// </summary>
	[Export(PropertyHint.Range, "1.0,1000.0")]
	public double HorizontalRotationSensitiveness { get; set; } = 10.0;

	/// <summary>
	/// Speed at which <see cref="ActiveCamera"/> will traslate between points. This will speed up or slow down any rotation and 
	/// </summary>
	[Export(PropertyHint.Range, "0.1,1")]
	public double CameraSpeed { get; set; } = 0.1;

	/// <summary>
	/// <see cref="ActiveCamera"/> horizontal rotation angle, independent of the calculated horizontal rotation angle.
	/// </summary>
	[Export]
	public double CameraHorizontalRotationDeg { get; set; } = -45;
}

// ------------------------------------
// Mouse
// ------------------------------------
public partial class TPCSharpCamera
{
	/// <summary>
	/// Enables mouse controls.
	/// </summary>
	[ExportGroup("Mouse")]
	[Export]
	public bool MouseFollow { get; set; } = false;

	/// <summary>
	/// Speed on which mouse input is processed on the X axis. A higher value will amplify the effect on the camera's position.
	/// </summary>
	[Export(PropertyHint.Range, "0.0,100.0")]
	public double MouseSensitivenessX { get; set; } = 1.0f;

	/// <summary>
	/// Speed on which mouse input is processed on the Y axis. A higher value will amplify the effect on the camera's position.
	/// </summary>
	[Export(PropertyHint.Range, "0.0,100.0")]
	public double MouseSensitivenessY { get; set; } = 1.0f;
}

// ------------------------------------
// CameraShake
// ------------------------------------
public partial class TPCSharpCamera
{
	/// <summary>
	/// Current CameraShake presets loaded into CameraShaker
	/// </summary>
	[ExportGroup("Camera Shake")]
	[Export]
	public Array<TPCShakePreset> ShakePresets { get; set; } // cannot auto init
}

// ------------------------------------
// SpringArm3D
// ------------------------------------
public partial class TPCSharpCamera
{
	/// <summary>
	/// Mask value belonging to <see cref="CameraSpringArm"/>. It will react to members with the same mask number.
	/// </summary>
	// Mapping Camera3D Properties
	[ExportCategory("SpringArm3D")]
	[Export]
	public int SpringArmCollissionMask { get; set; } = 1; // Set when _Ready();

	/// <summary>
	/// Property matching <see cref="SpringArm3D.Margin"/>.
	/// </summary>
	[Export(PropertyHint.Range, "0.0, 100.0, 0.01, or_greater, or_less, hide_slider, suffix:m")]
	public double SpringArmMargin { get; set; } = 0.01f; // Set when _Ready();

}

// ------------------------------------
// ActiveCamera properties
// ------------------------------------
public partial class TPCSharpCamera
{
	/// <summary>
	/// Mapped property of <see cref="Camera3D.Projection"/>
	/// </summary>
	[Export]
	public Camera3D.ProjectionType ProjectionType { get; set; } = Camera3D.ProjectionType.Perspective;

	/// <summary>
	/// Mapped propery of <see cref="Camera3D.Current"/>
	/// </summary>
	[Export]
	public bool Current { get; set; } // exposes the Current from Camera3D, mirror the value from Camera. Set on _Ready();
= false; // exposes the Current from Camera3D, mirror the value from Camera. Set on _Ready();

	/// <summary>
	/// Mapped property from <see cref="Camera3D.Fov"/>
	/// </summary>
	[Export(PropertyHint.Range, "1.0, 179.0, 0.1, suffix:�")]
	public double Fov { get; set; } = 75.0;

	/// <summary>
	/// Mapped property from <see cref="Camera3D.Near"/>
	/// </summary>
	[Export]
	public double Near { get; set; } = 0.05;

	/// <summary>
	/// Mapped property from <see cref="Camera3D.Far"/>
	/// </summary>
	[Export]
	public double Far { get; set; } = 4000.0;

	/// <summary>
	/// Mapped property from <see cref="Camera3D.KeepAspect"/>
	/// </summary>
	[ExportCategory("Camera3D")]
	[Export]
	public Camera3D.KeepAspectEnum KeepAspect { get; set; } = Camera3D.KeepAspectEnum.Height;

	/// <summary>
	/// Mapped property from <see cref="Camera3D.DopplerTracking"/>
	/// </summary>
	[Export]
	public Camera3D.DopplerTrackingEnum DopplerTracking { get; set; } = Camera3D.DopplerTrackingEnum.Disabled;

	/// <summary>
	/// <see cref="ActiveCamera"/> tilt degree.
	/// </summary>
	[Export(PropertyHint.Range, "1.0, 179.0, 0.1, suffix:�")]
	public double CameraTiltDeg { get; set; } = 0;

	/// <summary>
	/// Mapped property of <see cref="Camera3D.CullMask"/>
	/// </summary>
	[Export(PropertyHint.Layers3DRender)]
	public uint CullMask { get; set; } = 1048575;
}

// ------------------------------------
// Parent Scene properties
// ------------------------------------
public partial class TPCSharpCamera
{
	/// <summary>
	/// <see cref="Godot.Environment"/> loaded into <see cref="ActiveCamera"/> to render with.
	/// </summary>
	[Export]
	public Godot.Environment Environment { get; set; }

	/// <summary>
	/// Mapped property of <see cref="Camera3D.Attributes"/>.
	/// </summary>
	[Export]
	public CameraAttributes Attributes { get; set; }
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
	public Camera3D GetCamera() => ActiveCamera;

	/// <summary>
	/// Gets vector pointing in front of the <see cref="OffsetPivot"/>.
	/// </summary>
	/// <returns></returns>
	public Vector3 GetFrontDirection()
	{
		Vector3 direction = OffsetPivot.GlobalPosition - ActiveCamera.GlobalPosition;
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
			ActiveCamera = GetNode<Camera3D>("Camera");
			RotationPivot = GetNode<Node3D>("RotationPivot");
			OffsetPivot = GetNode<Node3D>("RotationPivot/OffsetPivot");
			CameraSpringArm = GetNode<SpringArm3D>("RotationPivot/OffsetPivot/CameraSpringArm");
			CameraMarker = GetNode<Marker3D>("RotationPivot/OffsetPivot/CameraSpringArm/CameraMarker");
			PivotDebug = GetNode<MeshInstance3D>("RotationPivot/OffsetPivot/PivotDebug");
			CameraShaker = GetNode<TPCShaker>("CameraShaker");
			ActiveCamera.TopLevel = true;
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
		// Camera3D properties
		Variant currentCamera = ActiveCamera.Get(Camera3D.PropertyName.Current);
		Current = currentCamera.AsBool();

		Variant aspect = ActiveCamera.Get(Camera3D.PropertyName.KeepAspect);
		KeepAspect = Enum.Parse<Camera3D.KeepAspectEnum>(aspect.AsString());

		Variant cullMask = ActiveCamera.Get(Camera3D.PropertyName.CullMask);
		CullMask = cullMask.AsUInt32();

		Variant doppler = ActiveCamera.Get(Camera3D.PropertyName.DopplerTracking);
		DopplerTracking = Enum.Parse<Camera3D.DopplerTrackingEnum>(doppler.AsString());

		Variant projectionType = ActiveCamera.Get(Camera3D.PropertyName.Projection);
		ProjectionType = Enum.Parse<Camera3D.ProjectionType>(projectionType.ToString());

		Variant fov = ActiveCamera.Get(Camera3D.PropertyName.Fov);
		Fov = fov.AsDouble();

		Variant near = ActiveCamera.Get(Camera3D.PropertyName.Near);
		Near = near.AsDouble();

		Variant far = ActiveCamera.Get(Camera3D.PropertyName.Far);
		Far = far.AsDouble();

		// SpringArm3D properties
		Variant collisionMask = CameraSpringArm.Get(SpringArm3D.PropertyName.CollisionMask);
		SpringArmCollissionMask = collisionMask.AsInt32();

		Variant margin = CameraSpringArm.Get(SpringArm3D.PropertyName.Margin);
		SpringArmMargin = margin.AsDouble();

		Variant distanceFromPivot = CameraSpringArm.Get(SpringArm3D.PropertyName.SpringLength);
		DistanceFromPivot = distanceFromPivot.AsDouble();
	}

	/// <summary>
	/// Calculates linear interpolation values for each axis, and updates the <see cref="ActiveCamera"/> global position. GDScript can do this in one line. On C#, each lerp for each axis must be listed on its own line.
	/// </summary>
	private void TweenCameraToMarker()
	{
		float lerpX = (float)Mathf.Lerp(ActiveCamera.GlobalPosition.X, CameraMarker.GlobalPosition.X, CameraSpeed);
		float lerpY = (float)Mathf.Lerp(ActiveCamera.GlobalPosition.Y, CameraMarker.GlobalPosition.Y, CameraSpeed);
		float lerpZ = (float)Mathf.Lerp(ActiveCamera.GlobalPosition.Z, CameraMarker.GlobalPosition.Z, CameraSpeed);

		ActiveCamera.GlobalPosition = new Vector3(x: lerpX, y: lerpY, z: lerpZ);
	}

	/// <summary>
	/// Writes the values inside TPC's internal fields to <see cref="ActiveCamera"/>. Usually executed at the start of every _Physics(); loop.
	/// </summary>
	private void UpdateCameraProperties()
	{
		ActiveCamera.KeepAspect = KeepAspect;
		ActiveCamera.CullMask = CullMask;
		ActiveCamera.DopplerTracking = DopplerTracking;
		ActiveCamera.Projection = ProjectionType;
		ActiveCamera.Fov = (float)Fov;
		ActiveCamera.Near = (float)Near;
		ActiveCamera.Far = (float)Far;
		if (ActiveCamera.Environment != Environment)
		{
			ActiveCamera.Environment = Environment;
		}

		if (ActiveCamera.Attributes != Attributes)
		{
			ActiveCamera.Attributes = Attributes;
		}
	}

	private void ProcessTiltInput()
	{
		if (InputMap.HasAction("tp_camera_right") && InputMap.HasAction("tp_camera_left"))
		{
			float cameraHorizontalRotationVariation = Input.GetActionStrength("tp_camera_right") - Input.GetActionStrength("tp_camera_left");
			cameraHorizontalRotationVariation = (float)cameraHorizontalRotationVariation * (float)GetProcessDeltaTime() * 30 * (float)HorizontalRotationSensitiveness;
			CameraHorizontalRotationDeg =+ CameraHorizontalRotationDeg;
		}
	}

	private void ProcessHorizontalRotationInput()
	{
		if (InputMap.HasAction("tp_camera_up") && InputMap.HasAction("tp_camera_down"))
		{
			float tiltVariation = Input.GetActionStrength("tp_camera_up") - Input.GetActionStrength("tp_camera_down");
			tiltVariation = tiltVariation * (float)GetProcessDeltaTime() * 5 * (float)VerticalTiltSensitiveness;
			CameraTiltDeg = Math.Clamp(CameraTiltDeg + tiltVariation, TiltLowerLimitDeg - InitialDiveAngleDeg, TiltUpperLimitDeg - InitialDiveAngleDeg);
		}
	}

	private void UpdateCameraTilt()
	{
		double tiltFinalVal = Mathf.Clamp(InitialDiveAngleDeg + CameraTiltDeg, TiltLowerLimitDeg, TiltUpperLimitDeg);
		Tween tween = CreateTween();
		tween.TweenProperty(ActiveCamera, "global_rotation_degrees:x", tiltFinalVal, 0.1);
	}

	private void UpdateCameraHorizontalRotation()
	{
		var tween = CreateTween();
		tween.TweenProperty(RotationPivot, "global_rotation_degrees:y", CameraHorizontalRotationDeg * -1, 0.1).AsRelative();
		CameraHorizontalRotationDeg = 0.0; // reset value

		Vector2 vectToOffsetPivot = (new Vector2(OffsetPivot.GlobalPosition.X, OffsetPivot.GlobalPosition.Z) - new Vector2(ActiveCamera.GlobalPosition.X, ActiveCamera.GlobalPosition.Z)).Normalized();

		var activeCameraGlobalRotation = ActiveCamera.GlobalRotation;
		activeCameraGlobalRotation.Y = -new Vector2(0.0f, -1.0f).AngleTo(vectToOffsetPivot.Normalized());
	}

	public void ApplyPresetShake(int presetNumber)
	{
		CameraShaker.ApplyPresetShake(ShakePresets[presetNumber]);
	}
}



#endif
