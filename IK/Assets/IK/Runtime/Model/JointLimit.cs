using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public struct JointLimit
    {
        public Vector2 xDegrees;
        public Vector2 yDegrees;
        public Vector2 zDegrees;

        public JointLimit(Vector2 xDegrees, Vector2 yDegrees, Vector2 zDegrees)
        {
            this.xDegrees = xDegrees;
            this.yDegrees = yDegrees;
            this.zDegrees = zDegrees;
        }

        public static JointLimit Default => new(
            new Vector2(-180f, 180f),
            new Vector2(-180f, 180f),
            new Vector2(-180f, 180f));
    }
}
