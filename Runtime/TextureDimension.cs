namespace Plugins.VFX.Volumes
{
	/// <summary>
	///   <para>Texture "dimension" (type).</para>
	/// </summary>
	public enum TextureDimension
	{
		/// <summary>
		///   <para>Texture type is not initialized or unknown.</para>
		/// </summary>
		Unknown = -1, // 0xFFFFFFFF
		/// <summary>
		///   <para>No texture is assigned.</para>
		/// </summary>
		None = 0,
		/// <summary>
		///   <para>Any texture type.</para>
		/// </summary>
		Any = 1,
		/// <summary>
		///   <para>2D texture (Texture2D).</para>
		/// </summary>
		Tex2D = 2,
		/// <summary>
		///   <para>3D volume texture (Texture3D).</para>
		/// </summary>
		Tex3D = 3,
		/// <summary>
		///   <para>Cubemap texture.</para>
		/// </summary>
		Cube = 4,
		/// <summary>
		///   <para>2D array texture (Texture2DArray).</para>
		/// </summary>
		Tex2DArray = 5,
		/// <summary>
		///   <para>Cubemap array texture (CubemapArray).</para>
		/// </summary>
		CubeArray = 6,
	}
}