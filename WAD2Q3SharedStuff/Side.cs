using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace WAD2Q3SharedStuff
{

	// From: https://github.com/sponticelli/LiteNinja-Common/
	public static class MathExtensions
	{
		/// <summary>
		/// Mod operator that also works for negative m.
		/// </summary>
		public static int FloorMod(int m, int n)
		{
			if (m >= 0)
			{
				return m % n;
			}

			return (m - 2 * m * n) % n;
		}
	}




	// Adapted from https://github.com/Dylancyclone/VMF2OBJ/

	public static class Vector3Extensions
	{

		public static bool pointInHull(this Vector3 point, Side[] sides)
		{

			foreach (Side side in sides)
			{
				Plane plane = new Plane(side);
				Vector3 facing = Vector3.Normalize(point - plane.center());

				if (Vector3.Dot(facing, Vector3.Normalize(plane.normal())) < -0.01f)
				{
					return false;
				}
			}

			return true;
		}
		public static Vector3? GetPlaneIntersectionPoint(Vector3[] side1, Vector3[] side2, Vector3[] side3)
		{
			Plane plane1 = new Plane(side1);
			Vector3 plane1Normal = Vector3.Normalize(plane1.normal());
			Plane plane2 = new Plane(side2);
			Vector3 plane2Normal = Vector3.Normalize(plane2.normal());
			Plane plane3 = new Plane(side3);
			Vector3 plane3Normal = Vector3.Normalize(plane3.normal());
			double determinant =
				(
					(
						plane1Normal.X * plane2Normal.Y * plane3Normal.Z +
						plane1Normal.Y * plane2Normal.Z * plane3Normal.X +
						plane1Normal.Z * plane2Normal.X * plane3Normal.Y
					)
					-
					(
						plane1Normal.Z * plane2Normal.Y * plane3Normal.X +
						plane1Normal.Y * plane2Normal.X * plane3Normal.Z +
						plane1Normal.X * plane2Normal.Z * plane3Normal.Y
					)
				);

			// Can't intersect parallel planes.

			if ((determinant <= 0.01 && determinant >= -0.01) || Double.IsNaN(determinant))
			{
				return null;
			}

			Vector3 point =
			(
				Vector3.Cross(plane2Normal, plane3Normal) * (float)plane1.distance()
				+ Vector3.Cross(plane3Normal, plane1Normal) * (float)plane2.distance()
				+ Vector3.Cross(plane1Normal, plane2Normal) * (float)plane3.distance()
			) / (float)determinant;

			return point;
		}

		public static Vector3 getLonger(this Vector3 vectorA, Vector3 vectorB)
		{
			return vectorA.Length() > vectorB.Length() ? vectorA : vectorB;
		}

		public static int closestIndex(this Vector3 vec, Vector3[] vectors)
		{
			if (vectors.Length == 0)
			{
				return -1;
			}
			else if (vectors.Length == 1)
			{
				return 0;
			}
			else
			{
				int index = 0;
				double distance = Vector3.Distance(vec, vectors[0]);
				for (int i = 1; i < vectors.Length; i++)
				{
					double thisDistance = Vector3.Distance(vec, vectors[i]);
					if (thisDistance < distance)
					{
						index = i;
						distance = thisDistance;
					}
				}
				return index;
			}
		}
	}

	public class VectorSorter
	{
		Vector3 center, normal, pp, qp;

		public VectorSorter(Vector3 normal, Vector3 center)
		{
			this.center = center;
			this.normal = normal;
			Vector3 i = Vector3.Cross(normal, new Vector3(1, 0, 0));
			Vector3 j = Vector3.Cross(normal, new Vector3(0, 1, 0));
			Vector3 k = Vector3.Cross(normal, new Vector3(0, 0, 1));
			pp = i.getLonger(j).getLonger(k); // Get longest to reduce floating point imprecision
			qp = Vector3.Cross(normal, pp);
		}

		public double getOrder(Vector3 vector)
		{
			Vector3 normalized = vector - center;
			return Math.Atan2(
				Vector3.Dot(normal, Vector3.Cross(normalized, pp)),
				Vector3.Dot(normal, Vector3.Cross(normalized, qp))
				);
		}
	}

	public class Plane
	{

		public Vector3 a;
		public Vector3 b;
		public Vector3 c;

		public Plane(Vector3 a, Vector3 b, Vector3 c)
		{
			this.a = a;
			this.b = b;
			this.c = c;
		}

		public Plane(Vector3[] points)
		{
			if (points.Length < 3)
			{
				throw new InvalidOperationException("Plane must have 3 points");
			}
			this.a = points[0];
			this.b = points[1];
			this.c = points[2];
		}

		public Plane(Side side)
		{
			if (side.points.Length < 3)
			{
				throw new InvalidOperationException("Plane must have 3 points");
			}
			this.a = side.points[0];
			this.b = side.points[1];
			this.c = side.points[2];
		}

		public Vector3 normal()
		{
			Vector3 ab = this.b - this.a;
			Vector3 ac = this.c - this.a;

			return Vector3.Cross(ab, ac);
		}

		public Vector3 center()
		{
			return (this.a + this.b + this.c) / 3.0f;
		}

		public double distance()
		{
			Vector3 normal = this.normal();

			return ((this.a.X * normal.X + (this.a.Y * normal.Y) + (this.a.Z * normal.Z))
				/ Math.Sqrt(Math.Pow(normal.X, 2.0f) + Math.Pow(normal.Y, 2.0f) + Math.Pow(normal.Z, 2.0f)));
		}

		public String toString()
		{
			return "(" + a + "," + b + "," + c + ")";
		}

	}

	public class Solid
	{
		//public String id;
		public Side[] sides;

		public static bool isDisplacementSolid(Solid solid)
		{
			foreach (Side side in solid.sides)
			{
				if (side.dispinfo != null)
				{
					return true;
				}
			}
			return false;
		}
	}
	public class Displacement
	{
		public int power;
		public Vector3 startposition;

		public Vector3[][] normals;
		public double[][] distances;
		public double[][] alphas;
	}
	public class Side
	{
		public String id;

		public String plane;
		public Vector3[] points;

		public String material;

		public String uaxis;
		public Vector3 uAxisVector;
		public double uAxisTranslation;
		public double uAxisScale;

		public String vaxis;
		public Vector3 vAxisVector;
		public double vAxisTranslation;
		public double vAxisScale;

		public Displacement dispinfo;

		public static Side completeSide(Side side, Solid solid)
		{
			List<Vector3> intersections = new List<Vector3>();

			foreach (Side side2 in solid.sides)
			{
				foreach (Side side3 in solid.sides)
				{
					Vector3? intersection = Vector3Extensions.GetPlaneIntersectionPoint(side.points, side2.points, side3.points);

					if (intersection == null)
					{
						continue;
					}

					if (intersections.Contains(intersection.Value))
					{
						continue;
					}

					if (!intersection.Value.pointInHull(solid.sides))
					{
						continue;
					}

					// If the intersection is close to an existing intersection
					bool alreadyExists = false;
					foreach (Vector3 existingIntersection in intersections)
					{
						if (Vector3.Distance(existingIntersection, intersection.Value) < 0.2)
						{
							alreadyExists = true;
							break;
						}
					}
					if (alreadyExists)
					{
						continue;
					}

					// If the intersection is close to an existing point on another side
					foreach (Side existingSide in solid.sides)
					{
						foreach (Vector3 existingPoint in existingSide.points)
						{
							if (Vector3.Distance(existingPoint, intersection.Value) < 0.2)
							{
								// Merge with the existing point
								intersection = existingPoint;
								break;
							}
						}
					}

					intersections.Add((intersection.Value));
				}
			}

			// Theoretically source only allows convex shapes, and fixes any problems upon
			// saving...

			if (intersections.Count < 3)
			{
				Console.WriteLine("Malformed side " + side.id + ", only " + intersections.Count + " points");
				return null;
			}

			Vector3 sum = new Vector3();
			foreach (Vector3 point in intersections)
			{
				sum += (point);
			}
			Vector3 center = sum / intersections.Count;
			Vector3 normal = Vector3.Normalize(new Plane(side).normal());

			List<Vector3> IntersectionsList = new List<Vector3>(intersections);
			VectorSorter sorter = new VectorSorter(normal, center);

			IntersectionsList.Sort((o1, o2) =>
			{
				return ((Double)sorter.getOrder(o1)).CompareTo((Double)sorter.getOrder(o2));
			});
			/*
			Collections.sort(IntersectionsList, new Comparator<Vector3>() {
				@Override
				public int compare(Vector3 o1, Vector3 o2)
				{
					return ((Double)sorter.getOrder(o1)).compareTo((Double)sorter.getOrder(o2));
				}
			});*/

			//Side newSide = VMF2OBJ.gson.fromJson(VMF2OBJ.gson.toJson(side, Side.class), Side.class);
			Side newSide = (Side)side.MemberwiseClone();

			newSide.points = IntersectionsList.ToArray(/*new Vector3[IntersectionsList.size()]*/);

			return newSide;
		}
	}
}
