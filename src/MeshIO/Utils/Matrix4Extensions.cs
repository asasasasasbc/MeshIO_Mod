using CSMath;

namespace MeshIO.Utils // Or CSMath.Extensions
{
    public static class Matrix4Extensions
    {
        /// <summary>
        /// Converts the CSMath.Matrix4 to a row-major double array (16 elements).
        /// Assumes CSMath.Matrix4 stores elements m<row><col>.
        /// </summary>
        public static double[] ToRowMajorArray(this Matrix4 matrix)
        {
            return new double[] {
                matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                matrix.m30, matrix.m31, matrix.m32, matrix.m33
            };
        }
    }
}