﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Squared.Util;
using Squared.Game;
using Microsoft.Xna.Framework;
using System.Collections;

namespace Squared.Game {
    [TestFixture]
    public class GeometryTests {
        public Polygon MakeSquare (float x, float y, float size) {
            size /= 2;

            return new Polygon(new Vector2[] {
                new Vector2(x - size, y - size),
                new Vector2(x + size, y - size),
                new Vector2(x + size, y + size),
                new Vector2(x - size, y + size)
            });
        }

        public Polygon MakeRectangle (float x1, float y1, float x2, float y2) {
            return new Polygon(new Vector2[] {
                new Vector2(x1, y1),
                new Vector2(x2, y1),
                new Vector2(x2, y2),
                new Vector2(x1, y2)
            });
        }

        [Test]
        public void ProjectOntoAxisTest () {
            var vertices = MakeSquare(0, 0, 5);

            var expected = new Interval(-2.5f, 2.5f);
            var interval = Geometry.ProjectOntoAxis(new Vector2(0.0f, 1.0f), vertices);

            Assert.AreEqual(expected, interval);

            expected = new Interval(-2.5f, 2.5f);
            interval = Geometry.ProjectOntoAxis(new Vector2(1.0f, 0.0f), vertices);

            Assert.AreEqual(expected, interval);

            var distance = (vertices[2] - vertices[0]).Length();
            expected = new Interval(-distance / 2.0f, distance / 2.0f);
            var vec = new Vector2(1.0f, 1.0f);
            vec.Normalize();
            interval = Geometry.ProjectOntoAxis(vec, vertices);

            Assert.AreEqual(expected, interval);
        }

        [Test]
        public void DoPolygonsIntersectTest () {
            Assert.IsTrue(Geometry.DoPolygonsIntersect(MakeSquare(0, 0, 5), MakeSquare(0, 0, 5)));
            Assert.IsTrue(Geometry.DoPolygonsIntersect(MakeSquare(0, 0, 5), MakeSquare(2, 0, 5)));
            Assert.IsTrue(Geometry.DoPolygonsIntersect(MakeSquare(0, 0, 5), MakeSquare(2, 2, 5)));
            Assert.IsTrue(Geometry.DoPolygonsIntersect(MakeSquare(2, 2, 5), MakeSquare(2, 0, 5)));
            Assert.IsTrue(Geometry.DoPolygonsIntersect(MakeSquare(2, 2, 5), MakeSquare(0, 0, 5)));
            Assert.IsTrue(Geometry.DoPolygonsIntersect(MakeSquare(1.9f, 1.9f, 2), MakeSquare(0, 0, 2)));

            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(2, 2, 2), MakeSquare(0, 0, 2)));
            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(3, 3, 5), MakeSquare(-3, -3, 5)));
            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(3, 3, 5), MakeSquare(-3, 0, 5)));
            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(3, 3, 5), MakeSquare(0, -3, 5)));
        }

        [Test]
        public void ResolvePolygonMotionTest () {
            var result = Geometry.ResolvePolygonMotion(MakeSquare(0, 0, 5), MakeSquare(0, 0, 5), new Vector2(0, 0));
            Assert.IsTrue(result.AreIntersecting);
            Assert.IsTrue(result.WouldHaveIntersected);
            Assert.IsTrue(result.WillBeIntersecting);

            result = Geometry.ResolvePolygonMotion(MakeSquare(0, 0, 5), MakeSquare(5.1f, 0, 5), new Vector2(-5, 0));
            Assert.IsFalse(result.AreIntersecting);
            Assert.IsFalse(result.WouldHaveIntersected);
            Assert.IsFalse(result.WillBeIntersecting);

            Assert.AreEqual(result.ResultVelocity, new Vector2(-5, 0));
            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(result.ResultVelocity.X, result.ResultVelocity.Y, 5), MakeSquare(5.1f, 0, 5)));

            result = Geometry.ResolvePolygonMotion(MakeSquare(0, 0, 5), MakeSquare(5.1f, 0, 5), new Vector2(5, 0));
            Assert.IsFalse(result.AreIntersecting);
            Assert.IsTrue(result.WouldHaveIntersected);
            Assert.IsFalse(result.WillBeIntersecting);

            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(result.ResultVelocity.X, result.ResultVelocity.Y, 5), MakeSquare(5.1f, 0, 5)));

            result = Geometry.ResolvePolygonMotion(MakeSquare(0, 0, 5), MakeSquare(5.1f, 5.1f, 5), new Vector2(5, 5));
            Assert.IsFalse(result.AreIntersecting);
            Assert.IsTrue(result.WouldHaveIntersected);
            Assert.IsFalse(result.WillBeIntersecting);

            Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(result.ResultVelocity.X, result.ResultVelocity.Y, 5), MakeSquare(5.1f, 5.1f, 5)));
        }

        [Test]
        public void MotionRoundingTest () {
            var a = MakeSquare(0, 0, 5);
            var b = MakeSquare(5, 5, 5);

            float x, y;

            x = y = 0.0f;

            for (x = 0; x < 5; x += 0.01f) {
                var result = Geometry.ResolvePolygonMotion(a, b, new Vector2(x, y));
                Assert.IsFalse(result.AreIntersecting);
                Assert.IsFalse(result.WouldHaveIntersected);
                Assert.IsFalse(result.WillBeIntersecting);

                Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(result.ResultVelocity.X, result.ResultVelocity.Y, 5), b));
            }

            x = y = 0.0f;

            for (y = 0; y < 5; y += 0.01f) {
                var result = Geometry.ResolvePolygonMotion(a, b, new Vector2(x, y));
                Assert.IsFalse(result.AreIntersecting);
                Assert.IsFalse(result.WouldHaveIntersected);
                Assert.IsFalse(result.WillBeIntersecting);

                Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(result.ResultVelocity.X, result.ResultVelocity.Y, 5), b));
            }

            x = y = 0.0f;

            for (float i = 0.01f; i <= 5; i += 0.01f) {
                x = y = i;
                var result = Geometry.ResolvePolygonMotion(a, b, new Vector2(x, y));
                Assert.IsFalse(result.AreIntersecting);
                Assert.IsTrue(result.WouldHaveIntersected);
                Assert.IsFalse(result.WillBeIntersecting);

                Assert.IsFalse(Geometry.DoPolygonsIntersect(MakeSquare(result.ResultVelocity.X, result.ResultVelocity.Y, 5), b));
            }
        }

        [Test]
        public void FPInsanityTest1 () {
            var a = MakeRectangle(1691.58f, 996.0f, 1759.58f, 1056.0f);
            var b = MakeRectangle(-744, 1056, 2560, 1112);
            var v = new Vector2(0.0f, 1.26f);

            for (float i = 0.01f; i <= 2.0f; i += 0.01f) {
                var j = v * i;
                a.Position = Vector2.Zero;
                Assert.IsFalse(Geometry.DoPolygonsIntersect(a, b));

                var result = Geometry.ResolvePolygonMotion(a, b, j);
                Assert.IsFalse(result.AreIntersecting);
                Assert.IsTrue(result.WouldHaveIntersected);
                Assert.IsFalse(result.WillBeIntersecting);

                a.Position = result.ResultVelocity;
                Assert.IsFalse(Geometry.DoPolygonsIntersect(a, b));
            }
        }

        [Test]
        public void PointInTriangleTest () {
            var tri = new Vector2[] { 
                new Vector2(0.0f, 0.0f),
                new Vector2(2.0f, 0.0f),
                new Vector2(0.0f, 2.0f)
            };

            Assert.IsTrue(Geometry.PointInTriangle(
                new Vector2(0.5f, 0.5f), tri
            ));

            Assert.IsFalse(Geometry.PointInTriangle(
                tri[0], tri
            ));
            Assert.IsFalse(Geometry.PointInTriangle(
                tri[1], tri
            ));
            Assert.IsFalse(Geometry.PointInTriangle(
                tri[2], tri
            ));

            Assert.IsFalse(Geometry.PointInTriangle(
                new Vector2(-1.0f, -1.0f), tri
            ));
            Assert.IsFalse(Geometry.PointInTriangle(
                new Vector2(-1.0f, 2.0f), tri
            ));
            Assert.IsFalse(Geometry.PointInTriangle(
                new Vector2(2.0f, -1.0f), tri
            ));
            Assert.IsFalse(Geometry.PointInTriangle(
                new Vector2(2.0f, 2.0f), tri
            ));
        }

        [Test]
        public void TriangulateTest () {
            var square = new Vector2[] {
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, 1.0f)
            };

            var triangles = Geometry.Triangulate(square).ToArray();
            Assert.AreEqual(2, triangles.Length);
            Assert.AreEqual(3, triangles[0].Length);
            Assert.AreEqual(3, triangles[1].Length);
            Assert.AreEqual(square[3], triangles[0][0]);
            Assert.AreEqual(square[0], triangles[0][1]);
            Assert.AreEqual(square[1], triangles[0][2]);
            Assert.AreEqual(square[1], triangles[1][0]);
            Assert.AreEqual(square[2], triangles[1][1]);
            Assert.AreEqual(square[3], triangles[1][2]);
        }

        [Test]
        public void GetBoundsTest () {
            var square = MakeSquare(5, 5, 10);
            var bounds = square.Bounds;

            Assert.AreEqual(new Vector2(0, 0), bounds.TopLeft);
            Assert.AreEqual(new Vector2(10, 10), bounds.BottomRight);
        }

        [Test]
        public void ClosestPointOnLineTest () {
            var pt1 = new Vector2(5, 5);
            var pt2 = new Vector2(10, 5);

            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(5, 0), pt1, pt2), pt1);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(0, 0), pt1, pt2), pt1);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(0, 5), pt1, pt2), pt1);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(0, 10), pt1, pt2), pt1);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(5, 10), pt1, pt2), pt1);

            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(10, 0), pt1, pt2), pt2);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(15, 0), pt1, pt2), pt2);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(15, 5), pt1, pt2), pt2);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(15, 10), pt1, pt2), pt2);
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(10, 10), pt1, pt2), pt2);

            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(7.5f, 2.5f), pt1, pt2), new Vector2(7.5f, 5));
            Assert.AreEqual(Geometry.ClosestPointOnLine(new Vector2(7.5f, 7.5f), pt1, pt2), new Vector2(7.5f, 5));
        }
    }

    public class BoundedObject : IHasBounds {
        public Bounds Bounds {
            get;
            set;
        }

        public BoundedObject (Vector2 tl, Vector2 br) {
            Bounds = new Bounds(tl, br);
        }

        public override string ToString () {
            return String.Format("{0}", Bounds);
        }

        public struct Comparer : IComparer, IComparer<BoundedObject> {
            public int Compare (object x, object y) {
                return (x.GetHashCode().CompareTo(y.GetHashCode()));
            }

            public int Compare (BoundedObject x, BoundedObject y) {
                return (x.GetHashCode().CompareTo(y.GetHashCode()));
            }
        }
    }

    public class BoundedObjectWithParentList : BoundedObject, ISpatialCollectionChild {
        public readonly List<WeakReference> Parents = new List<WeakReference>();

        public BoundedObjectWithParentList (Vector2 tl, Vector2 br)
            : base (tl, br) {
        }

        public void AddedToCollection (WeakReference collection) {
            Parents.Add(collection);
        }

        public void RemovedFromCollection (WeakReference collection) {
            if (!Parents.Remove(collection))
                throw new InvalidOperationException();
        }
    }

    [TestFixture]
    public class SpatialCollectionTests {
        public SpatialCollection<BoundedObject> Collection;

        [SetUp]
        public void SetUp () {
            Collection = new SpatialCollection<BoundedObject>(16);
        }

        internal BoundedObject[] Sorted (params BoundedObject[] arr) {
            var result = new BoundedObject[arr.Length];
            Array.Copy(arr, result, arr.Length);
            Array.Sort(result, new BoundedObject.Comparer());
            return result;
        }

        internal BoundedObject[] Sorted (params SpatialCollection<BoundedObject>.ItemInfo[] arr) {
            var result = new BoundedObject[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                result[i] = arr[i].Item;
            Array.Sort(result, new BoundedObject.Comparer());
            return result;
        }

        [Test]
        public void BasicTest () {
            var a = new BoundedObject(new Vector2(0, 0), new Vector2(15, 15));
            var b = new BoundedObject(new Vector2(8, 8), new Vector2(23, 23));
            var c = new BoundedObject(new Vector2(16, 16), new Vector2(31, 31));

            Collection.Add(a);
            Collection.Add(b);
            Collection.Add(c);

            Assert.AreEqual(Sorted(a, b, c), Sorted(Collection.ToArray()));

            Assert.AreEqual(Sorted( a, b ), Sorted(Collection.GetItemsFromBounds(a.Bounds).ToArray()));
            Assert.AreEqual(Sorted( a, b, c ), Sorted(Collection.GetItemsFromBounds(b.Bounds).ToArray()));
            Assert.AreEqual(Sorted( b, c ), Sorted(Collection.GetItemsFromBounds(c.Bounds).ToArray()));

            a.Bounds = new Bounds(new Vector2(24, 24), new Vector2(47, 47));
            Collection.UpdateItemBounds(a);

            Assert.AreEqual(Sorted( b, c, a ), Sorted(Collection.GetItemsFromBounds(a.Bounds).ToArray()));
            Assert.AreEqual(Sorted( b, c, a ), Sorted(Collection.GetItemsFromBounds(b.Bounds).ToArray()));
            Assert.AreEqual(Sorted( b, c, a ), Sorted(Collection.GetItemsFromBounds(c.Bounds).ToArray()));
        }

        [Test]
        public void RecursionTest () {
            var a = new BoundedObject(new Vector2(0, 0), new Vector2(15, 15));
            var b = new BoundedObject(new Vector2(8, 8), new Vector2(23, 23));
            var c = new BoundedObject(new Vector2(16, 16), new Vector2(31, 31));

            Collection.Add(a);
            Collection.Add(b);
            Collection.Add(c);

            var e1 = Collection.GetItemsFromBounds(new Bounds(new Vector2(8, 8), new Vector2(32, 32)));
            var e2 = Collection.GetItemsFromBounds(new Bounds(new Vector2(8, 8), new Vector2(32, 32)));
            var e3 = Collection.GetItemsFromBounds(new Bounds(new Vector2(8, 8), new Vector2(32, 32)));
            var arr = Sorted(e3.ToArray());
            Assert.AreEqual(Sorted(e2.ToArray()), arr);
            Assert.AreEqual(Sorted(e1.ToArray()), arr);
            Assert.AreEqual(Sorted(Collection.ToArray()), arr);
            e3.Dispose();
            e2.Dispose();
            e1.Dispose();
        }

        [Test]
        public void NotifyAddRemoveTest () {
            var a = new BoundedObjectWithParentList(new Vector2(0, 0), new Vector2(15, 15));
            var b = new BoundedObjectWithParentList(new Vector2(8, 8), new Vector2(23, 23));

            Assert.AreEqual(0, a.Parents.Count);
            Assert.AreEqual(0, b.Parents.Count);

            Collection.Add(a);
            Collection.Add(b);

            Assert.AreEqual(1, a.Parents.Count);
            Assert.AreEqual(1, b.Parents.Count);

            Assert.AreEqual(Collection, a.Parents[0].Target);
            Assert.AreEqual(Collection, b.Parents[0].Target);

            Collection.Remove(a);

            Assert.AreEqual(0, a.Parents.Count);
            Assert.AreEqual(1, b.Parents.Count);

            Collection.Remove(b);

            Assert.AreEqual(0, a.Parents.Count);
            Assert.AreEqual(0, b.Parents.Count);
        }

        [Test]
        public void UpdateItemBoundsDoesNotCallAddRemoveNotificationMethods () {
            var c2 = new SpatialCollection<BoundedObject>();
            var c3 = new SpatialCollection<BoundedObject>();
            var a = new BoundedObjectWithParentList(new Vector2(0, 0), new Vector2(15, 15));

            Collection.Add(a);
            c2.Add(a);
            c3.Add(a);

            a.Bounds = a.Bounds.Translate(Vector2.One * 128f);

            foreach (var collection in a.Parents) {
                var strongCollection = (SpatialCollection<BoundedObject>)collection.Target;
                strongCollection.UpdateItemBounds(a);
            }
        }

        [Test]
        public void ClearNotifiesRemovalTest () {
            var a = new BoundedObjectWithParentList(new Vector2(0, 0), new Vector2(15, 15));
            var b = new BoundedObjectWithParentList(new Vector2(8, 8), new Vector2(23, 23));

            Assert.AreEqual(0, a.Parents.Count);
            Assert.AreEqual(0, b.Parents.Count);

            Collection.Add(a);
            Collection.Add(b);

            Assert.AreEqual(1, a.Parents.Count);
            Assert.AreEqual(1, b.Parents.Count);

            Collection.Clear();

            Assert.AreEqual(0, a.Parents.Count);
            Assert.AreEqual(0, b.Parents.Count);
        }
    }
}
