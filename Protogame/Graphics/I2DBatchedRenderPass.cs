﻿using Microsoft.Xna.Framework.Graphics;

namespace Protogame
{
    /// <summary>
    /// A render pass in which graphics rendering is configured for an
    /// orthographic view.  When this render pass is active, the X and
    /// Y positions of entities map directly to the X and Y positions 
    /// of the game window, with (0, 0) being located in the top-left.
    /// <para>
    /// During this render pass, all texture render calls are batched
    /// together with a <see cref="SpriteBatch" />, and flushed at the
    /// end of the render pass.
    /// </para>
    /// </summary>
    /// <module>Graphics</module>
    public interface I2DBatchedRenderPass : IRenderPass, IRenderPassWithViewport
    {
        /// <summary>
        /// Gets or sets the sorting mode for textures during
        /// batch rendering.
        /// <para>
        /// This value is used as the sorting mode for the underlying
        /// <see cref="SpriteBatch"/>.
        /// </para>
        /// </summary>
        /// <value>The sorting mode to use when rendering textures.</value>
        SpriteSortMode TextureSortMode { get; set; }
    }
}

