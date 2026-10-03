using NUnit.Framework;
using TranslucentUIFX.Editor;
using UnityEngine;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassFusionDiagramTests
    {
        private GameObject root;
        private TranslucentGlassGroup group;
        [SetUp] public void Setup()
        {
            root = new GameObject("Composition map fixture", typeof(RectTransform), typeof(CanvasRenderer));
            group = root.AddComponent<TranslucentGlassGroup>();
            group.SurfacePadding = 0; group.FusionSoftness = 0;
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        private TranslucentGlassMember Member(string name, Vector2 position, Vector2 size, GlassOperation operation)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(root.transform, false);
            var member = go.AddComponent<TranslucentGlassMember>();
            member.RectTransform.anchoredPosition = position; member.RectTransform.sizeDelta = size;
            member.Shape = EdgeShape.Rectangle; member.Operation = operation; return member;
        }
        [Test] public void MapSubtractsFromTheWholeUnionRegardlessOfSiblingOrder()
        {
            var cut = Member("Hole", Vector2.zero, Vector2.one * 12f, GlassOperation.Cutout);
            Member("Left", Vector2.left * 15f, Vector2.one * 50f, GlassOperation.Add);
            Member("Right", Vector2.right * 15f, Vector2.one * 50f, GlassOperation.Add);
            group.RefreshMembers();
            float before = GlassFusionDiagram.SampleDistance(group, Vector2.zero);
            Assert.Greater(before, 0);
            Assert.Less(GlassFusionDiagram.SampleDistance(group, Vector2.right * 20f), 0);
            cut.transform.SetAsLastSibling(); group.RefreshMembers();
            Assert.AreEqual(before, GlassFusionDiagram.SampleDistance(group, Vector2.zero), 0.001f);
        }
        [Test] public void MapShowsSoftBridgesAndUpdatesExcludedMembers()
        {
            Member("Left", Vector2.left * 17f, Vector2.one * 30f, GlassOperation.Add);
            var right = Member("Right", Vector2.right * 17f, Vector2.one * 30f, GlassOperation.Add);
            group.RefreshMembers();
            Assert.Greater(GlassFusionDiagram.SampleDistance(group, Vector2.zero), 0);
            group.FusionSoftness = 16;
            Assert.Less(GlassFusionDiagram.SampleDistance(group, Vector2.zero), 0);
            right.Include = false; group.RefreshMembers();
            Assert.Greater(GlassFusionDiagram.SampleDistance(group, Vector2.zero), 0);
        }
        [Test] public void MapUsesMemberTransformsAndDoesNotMutateSceneData()
        {
            var member = Member("Rotated", new Vector2(20, 10), new Vector2(70, 10), GlassOperation.Add);
            member.transform.localRotation = Quaternion.Euler(0, 0, 90);
            group.RefreshMembers();
            string before = UnityEditor.EditorJsonUtility.ToJson(member);
            Assert.Less(GlassFusionDiagram.SampleDistance(group, new Vector2(20, 35)), 0);
            Assert.Greater(GlassFusionDiagram.SampleDistance(group, new Vector2(45, 10)), 0);
            Assert.AreEqual(before, UnityEditor.EditorJsonUtility.ToJson(member));
        }
        [Test] public void CutoutsAloneLeaveTheMapEmpty()
        {
            Member("Hole", Vector2.zero, Vector2.one * 100, GlassOperation.Cutout); group.RefreshMembers();
            Assert.Greater(GlassFusionDiagram.SampleDistance(group, Vector2.zero), 0);
        }
    }
}
