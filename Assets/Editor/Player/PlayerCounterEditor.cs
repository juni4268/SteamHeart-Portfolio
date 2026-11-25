using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerCounter))]
public class PlayerCounterEditor : PlayerSkillPolygonEditor
{
    protected override string SkillLabel => "Counter";
    protected override Color PolygonColor => Color.yellow;
    protected override string VertexColorName => "yellow";
}
