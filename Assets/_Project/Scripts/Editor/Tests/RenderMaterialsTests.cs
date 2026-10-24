using NUnit.Framework;
using UnityEngine;
using Crashverse.Core;

namespace Crashverse.EditorTests
{
    /// <summary>BUG-23 regression (EditMode): the shipped builder material is a supported URP shader, and Apply never leaves a renderer magenta.</summary>
    public class RenderMaterialsTests
    {
        static void AssertUsable(Material m, string what)
        {
            Assert.IsNotNull(m, what);
            Assert.IsNotNull(m.shader, what + " shader");
            Assert.AreNotEqual("Hidden/InternalErrorShader", m.shader.name, what);
            Assert.AreNotEqual("Standard", m.shader.name, what);
            Assert.IsTrue(m.shader.isSupported, what + ": " + m.shader.name + " not supported");
            StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name, what);
        }

        [Test] public void ShippedMaterialIsInResourcesAndSupported()
            => AssertUsable(Resources.Load<Material>("Materials/CV_SimpleLit"), "Resources/Materials/CV_SimpleLit");

        [Test] public void LitIsNeverNull() => AssertUsable(RenderMaterials.Lit, "RenderMaterials.Lit");

        [Test] public void PrimitiveAndProceduralRenderersGetIt()
        {
            var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var proc = new GameObject("proc"); proc.AddComponent<MeshFilter>(); var pr = proc.AddComponent<MeshRenderer>();
            proc.transform.SetParent(prim.transform);
            try
            {
                RenderMaterials.Apply(prim); RenderMaterials.Apply(pr);
                AssertUsable(prim.GetComponent<Renderer>().sharedMaterial, "primitive");
                AssertUsable(pr.sharedMaterial, "procedural MeshRenderer");
                Assert.AreEqual(0, RenderMaterials.CountBroken(prim));
            }
            finally { Object.DestroyImmediate(prim); }
        }
    }
}
