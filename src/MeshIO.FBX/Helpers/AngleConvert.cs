using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics; // For a simple Vector3, or you can define your own.
                       // If you're in Unity, use UnityEngine.Vector3 and Mathf.
namespace MeshIO.FBX.Helpers
{
    // If not in Unity, define a basic Vector3.
    // If in Unity, remove this and use UnityEngine.Vector3.
    // For simplicity, I'll use System.Numerics.Vector3 here.
    // However, System.Numerics.Vector3 might not have X, Y, Z named fields directly
    // depending on the .NET version/target, or they might be properties.
    // Let's assume a simple custom Vector3 for clarity with X, Y, Z public fields.

    public struct MyVector3 // Renamed to avoid conflict if System.Numerics is also used
    {
        public float X;
        public float Y;
        public float Z;

        public MyVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X:F3}, {Y:F3}, {Z:F3})";
        }
    }

    public enum RotationOrder
    {
        XYZ,
        XZY,
        YXZ,
        YZX,
        ZXY,
        ZYX
    }

    public class Matrix3D
    {
        public float[,] value;

        public Matrix3D()
        {
            value = new float[4, 4];
            // Initialize to identity
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    value[i, j] = (i == j) ? 1.0f : 0.0f;
        }

        public static Matrix3D Identity => new Matrix3D();

        private static float Deg2Rad(float degrees) => (float)(degrees / 180.0 * Math.PI);
        public static float Rad2Deg(float radians) => (float)(radians * 180.0 / Math.PI);
        private static float C(float rad) => (float)Math.Cos(rad);
        private static float S(float rad) => (float)Math.Sin(rad);

        public static Matrix3D generateRotXMatrix(float degrees)
        {
            float rad = Deg2Rad(degrees);
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            {
            {1, 0,     0,      0 },
            {0, C(rad),-S(rad),0 },
            {0, S(rad),C(rad), 0 },
            {0, 0,     0,      1 }
            };
            return m;
        }

        public static Matrix3D generateRotYMatrix(float degrees)
        {
            float rad = Deg2Rad(degrees);
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            {
            {C(rad),0,S(rad),0 },
            {0,     1,0,     0 },
            {-S(rad),0,C(rad),0 },
            {0,     0,0,     1 }
            };
            return m;
        }

        public static Matrix3D generateRotZMatrix(float degrees)
        {
            float rad = Deg2Rad(degrees);
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            {
            {C(rad),-S(rad),0,0 },
            {S(rad),C(rad), 0,0 },
            {0,     0,      1,0 },
            {0,     0,      0,1 }
            };
            return m;
        }

        // Matrix multiplication (A * B)
        public static Matrix3D operator *(Matrix3D a, Matrix3D b)
        {
            Matrix3D result = new Matrix3D();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result.value[i, j] = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        result.value[i, j] += a.value[i, k] * b.value[k, j];
                    }
                }
            }
            return result;
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < 4; ++i)
            {
                sb.AppendLine($"[{value[i, 0]:F3}, {value[i, 1]:F3}, {value[i, 2]:F3}, {value[i, 3]:F3}]");
            }
            return sb.ToString();
        }
    }

    public static class EulerAngleConverter
    {
        private const float Epsilon = 1e-6f; // For gimbal lock checks

        // Helper to construct the rotation matrix from Euler angles and a given order
        private static Matrix3D BuildRotationMatrix(MyVector3 eulerAnglesDegrees, RotationOrder order)
        {
            Matrix3D rx = Matrix3D.generateRotXMatrix(eulerAnglesDegrees.X);
            Matrix3D ry = Matrix3D.generateRotYMatrix(eulerAnglesDegrees.Y);
            Matrix3D rz = Matrix3D.generateRotZMatrix(eulerAnglesDegrees.Z);

            // The multiplication order defines the rotation.
            // E.g., XYZ means R = Rx * Ry * Rz (apply Rz, then Ry, then Rx in fixed frame,
            // or Rx, then Ry, then Rz in intrinsic moving frame)
            // Your reference: if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry * rz)); }
            // This implies the rotation part is (Rx * (Ry * Rz)). Let's stick to that.
            switch (order)
            {
                case RotationOrder.XYZ: return rx * ry * rz;
                case RotationOrder.XZY: return rx * rz * ry;
                case RotationOrder.YXZ: return ry * rx * rz;
                case RotationOrder.YZX: return ry * rz * rx;
                case RotationOrder.ZXY: return rz * rx * ry;
                case RotationOrder.ZYX: return rz * ry * rx;
                default: throw new ArgumentException("Invalid rotation order", nameof(order));
            }
        }

        // Helper to extract Euler angles from a rotation matrix for a given order
        // This is the complex part with many formulas.
        // Formulas adapted from: http://www.gregslabaugh.com/publications/euler.pdf
        // And also common knowledge from various graphics programming resources.
        // Note: Matrix elements are m[row, col] (0-indexed)
        private static MyVector3 ExtractEulerAngles(Matrix3D m, RotationOrder order)
        {
            float r11 = m.value[0, 0], r12 = m.value[0, 1], r13 = m.value[0, 2];
            float r21 = m.value[1, 0], r22 = m.value[1, 1], r23 = m.value[1, 2];
            float r31 = m.value[2, 0], r32 = m.value[2, 1], r33 = m.value[2, 2];

            float xRad = 0, yRad = 0, zRad = 0;

            switch (order)
            {
                case RotationOrder.XYZ: // R = Rx Ry Rz
                                        // y = asin(r13)
                                        // x = atan2(-r23/cos(y), r33/cos(y))
                                        // z = atan2(-r12/cos(y), r11/cos(y))
                    yRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, r13)));
                    if (Math.Abs(r13) < 1.0f - Epsilon) // Not in gimbal lock
                    {
                        xRad = (float)Math.Atan2(-r23, r33);
                        zRad = (float)Math.Atan2(-r12, r11);
                    }
                    else // Gimbal lock
                    {
                        zRad = 0; // Conventionally set z to 0
                        if (r13 > 0) // y = +PI/2
                            xRad = (float)Math.Atan2(r21, r22);
                        else // y = -PI/2
                            xRad = (float)Math.Atan2(-r21, -r22); // or Math.Atan2(r21, -r22) depending on convention
                    }
                    break;

                case RotationOrder.XZY: // R = Rx Rz Ry
                                        // z = asin(-r12)
                                        // x = atan2(r32/cos(z), r22/cos(z))
                                        // y = atan2(r13/cos(z), r11/cos(z))
                    zRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, -r12)));
                    if (Math.Abs(r12) < 1.0f - Epsilon)
                    {
                        xRad = (float)Math.Atan2(r32, r22);
                        yRad = (float)Math.Atan2(r13, r11);
                    }
                    else
                    {
                        yRad = 0;
                        if (-r12 > 0) // z = +PI/2
                            xRad = (float)Math.Atan2(-r31, r33); // Check r31, r33 based on matrix structure
                        else // z = -PI/2
                            xRad = (float)Math.Atan2(r31, r33);
                    }
                    break;

                case RotationOrder.YXZ: // R = Ry Rx Rz
                                        // x = asin(-r23)
                                        // y = atan2(r13/cos(x), r33/cos(x))
                                        // z = atan2(r21/cos(x), r22/cos(x))
                    xRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, -r23)));
                    if (Math.Abs(r23) < 1.0f - Epsilon)
                    {
                        yRad = (float)Math.Atan2(r13, r33);
                        zRad = (float)Math.Atan2(r21, r22);
                    }
                    else
                    {
                        zRad = 0;
                        if (-r23 > 0) // x = +PI/2
                            yRad = (float)Math.Atan2(-r12, r11); // or -r12, r11 or similar, depending on exact matrix
                        else // x = -PI/2
                            yRad = (float)Math.Atan2(r12, r11);
                    }
                    break;

                case RotationOrder.YZX: // R = Ry Rz Rx
                                        // z = asin(r21)
                                        // y = atan2(-r31/cos(z), r11/cos(z))
                                        // x = atan2(-r23/cos(z), r22/cos(z))
                    zRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, r21)));
                    if (Math.Abs(r21) < 1.0f - Epsilon)
                    {
                        yRad = (float)Math.Atan2(-r31, r11);
                        xRad = (float)Math.Atan2(-r23, r22);
                    }
                    else
                    {
                        xRad = 0;
                        if (r21 > 0) // z = +PI/2
                            yRad = (float)Math.Atan2(r32, r33); // or r32, r33 etc.
                        else // z = -PI/2
                            yRad = (float)Math.Atan2(-r32, -r33);
                    }
                    break;

                case RotationOrder.ZXY: // R = Rz Rx Ry
                                        // x = asin(r32)
                                        // z = atan2(-r12/cos(x), r22/cos(x))
                                        // y = atan2(-r31/cos(x), r33/cos(x))
                    xRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, r32)));
                    if (Math.Abs(r32) < 1.0f - Epsilon)
                    {
                        zRad = (float)Math.Atan2(-r12, r22);
                        yRad = (float)Math.Atan2(-r31, r33);
                    }
                    else
                    {
                        yRad = 0;
                        if (r32 > 0) // x = +PI/2
                            zRad = (float)Math.Atan2(r13, r11);
                        else // x = -PI/2
                            zRad = (float)Math.Atan2(-r13, -r11);
                    }
                    break;

                case RotationOrder.ZYX: // R = Rz Ry Rx (Common "yaw, pitch, roll")
                                        // y = asin(-r31)
                                        // z = atan2(r21/cos(y), r11/cos(y))
                                        // x = atan2(r32/cos(y), r33/cos(y))
                    yRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, -r31)));
                    if (Math.Abs(r31) < 1.0f - Epsilon)
                    {
                        zRad = (float)Math.Atan2(r21, r11);
                        xRad = (float)Math.Atan2(r32, r33);
                    }
                    else
                    {
                        xRad = 0;
                        if (-r31 > 0) // y = +PI/2 (pitch up)
                            zRad = (float)Math.Atan2(-r12, -r13); // or r12, r22 etc.
                        else // y = -PI/2 (pitch down)
                            zRad = (float)Math.Atan2(r12, r13); // or -r12, r22 etc. Gimbal lock formulas need careful derivation.
                                                                // For ZYX, if y = +/-90: z = atan2(m[0,1], m[0,2]) is common if x=0.
                                                                // Or (from Diebel "Representing Attitude"):
                                                                // if y = +PI/2, z = 0, x = atan2(r12, r22)
                                                                // if y = -PI/2, z = 0, x = -atan2(r12, r22)
                                                                // Let's use one of these common conventions.
                                                                // Setting x=0:
                                                                // if (-r31 > 0.99999) // y = 90
                                                                //     zRad = (float)Math.Atan2(r12, r22); (No, this is for r12 from matrix Ry(90)RxRz)
                                                                // else // y = -90
                                                                //     zRad = (float)Math.Atan2(-r12, r22);
                                                                // The Slabaugh paper (and many others) for ZYX (his order 12):
                                                                // if y = PI/2: x=0, z=atan2(r12,r22)
                                                                // if y = -PI/2: x=0, z=atan2(-r12,-r22)
                        if (-r31 > 0)  // y = +PI/2
                        {
                            xRad = 0; // Convention
                            zRad = (float)Math.Atan2(r12, r22); // This combination appears in ZYX from matrix multiplication for gimbal lock
                        }
                        else // y = -PI/2
                        {
                            xRad = 0; // Convention
                            zRad = (float)Math.Atan2(-r12, -r22);
                        }
                    }
                    break;

                default:
                    throw new ArgumentException("Invalid rotation order for extraction.", nameof(order));
            }

            return new MyVector3(Matrix3D.Rad2Deg(xRad), Matrix3D.Rad2Deg(yRad), Matrix3D.Rad2Deg(zRad));
        }


        /// <summary>
        /// Converts Euler angles from one rotation order to another.
        /// </summary>
        /// <param name="eulerAnglesDegrees">Input Euler angles in degrees.</param>
        /// <param name="sourceOrder">The rotation order of the input Euler angles.</param>
        /// <param name="targetOrder">The desired rotation order for the output Euler angles.</param>
        /// <returns>Euler angles in degrees in the target rotation order, representing the same orientation.</returns>
        public static MyVector3 ConvertRotationOrder(MyVector3 eulerAnglesDegrees, RotationOrder sourceOrder, RotationOrder targetOrder)
        {
            if (sourceOrder == targetOrder)
            {
                return eulerAnglesDegrees; // No conversion needed
            }

            // 1. Build the rotation matrix from the source Euler angles and source order
            Matrix3D rotationMatrix = BuildRotationMatrix(eulerAnglesDegrees, sourceOrder);

            // 2. Extract Euler angles from the rotation matrix using the target order
            MyVector3 targetEulerAngles = ExtractEulerAngles(rotationMatrix, targetOrder);

            return targetEulerAngles;
        }
    }


}