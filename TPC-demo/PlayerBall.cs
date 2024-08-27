using Godot;
using Microsoft.VisualBasic;
using System.Linq;

public partial class PlayerBall : RigidBody3D
{
    [Export]
    public int Speed { get; set; } = 10;

    public TPCSharpCamera Camera { get; set; }

    public override void _Ready()
    {
        CallDeferred(MethodName.LoadCamera);
    }

    private void LoadCamera()
    {
        Camera = GetNode<TPCSharpCamera>("ThirdPersonCamera");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionPressed("front"))
        {
            var dir = Camera.GetFrontDirection();
            ApplyCentralForce(dir * Speed);
        }

        if (Input.IsActionJustPressed("back"))
        {
            var dir = Camera.GetBackDirection();
            ApplyCentralForce(dir * Speed);
        }

        if (Input.IsActionJustPressed("left"))
        {
            var dir = Camera.GetLeftDirection();
            ApplyCentralForce(dir * Speed);
        }

        if (Input.IsActionJustPressed("right"))
        {
            var dir = Camera.GetRightDirection();
            ApplyCentralForce(dir * Speed);
        }

        if (Input.IsActionJustPressed("camera_shake"))
        {
            Camera.ApplyPresetShake(0);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_mouse_mode"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
                Camera.MouseFollow = false;
            }
            else
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                Camera.MouseFollow = true;
            }
        }
    }
}