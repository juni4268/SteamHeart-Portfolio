using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerGroundSlam))]
public class PlayerGroundSlamEditor : PlayerSkillPolygonEditor
{
    protected override string SkillLabel => "Ground Slam";
    protected override Color PolygonColor => Color.cyan;
    protected override string VertexColorName => "cyan";
}
