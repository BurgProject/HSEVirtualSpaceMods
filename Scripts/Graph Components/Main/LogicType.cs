namespace HSE.Components.Action
{
    public enum LogicType: byte
    {
        None = 0,
        WaypointMovement = 1,
        RotateToWaypoint = 2,
        RotateAroundAxis = 3,
        RotateAroundPoint = 4,
        SimpleAnimation = 5,
        Sound = 6
    }
}
