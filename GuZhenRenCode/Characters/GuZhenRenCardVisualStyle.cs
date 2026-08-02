using Godot;

namespace GuZhenRen.Characters;

/// <summary>
/// Shared monochrome presentation for every Gu Zhen Ren card pool.
///
/// The frame material is created in code so a DLL-only update does not keep
/// using an older PCK material. Dark source details and the outer edge become
/// black ink, while the remaining card body becomes neutral gray paper.
/// </summary>
internal static class GuZhenRenCardVisualStyle
{
    public static readonly Color CardBackgroundColor =
        new(0.42f, 0.42f, 0.42f, 1f);

    private static ShaderMaterial? _frameMaterial;

    public static Material FrameMaterial =>
        _frameMaterial ??= CreateFrameMaterial();

    private static ShaderMaterial CreateFrameMaterial()
    {
        Shader shader = new()
        {
            Code = """
                shader_type canvas_item;
                render_mode unshaded;

                // Keep the vanilla frame-material compatibility parameters.
                // Deck-view controls copy these values from the pool material.
                uniform float h = 0.0;
                uniform float s = 0.0;
                uniform float v = 1.0;

                uniform vec4 frame_color : source_color = vec4(0.01, 0.01, 0.01, 1.0);
                uniform vec4 background_color : source_color = vec4(0.42, 0.42, 0.42, 1.0);

                void fragment()
                {
                    vec4 source = texture(TEXTURE, UV);
                    vec4 vertex_color = COLOR;

                    float luminance = dot(
                        source.rgb,
                        vec3(0.2126, 0.7152, 0.0722)
                    );

                    vec2 nearest_edge = min(UV, vec2(1.0) - UV);
                    float edge_distance = min(nearest_edge.x, nearest_edge.y);
                    float outer_frame = 1.0 - smoothstep(0.035, 0.105, edge_distance);
                    float ink_detail = 1.0 - smoothstep(0.18, 0.70, luminance);
                    float frame_mask = clamp(
                        max(outer_frame, ink_detail * 0.82),
                        0.0,
                        1.0
                    );

                    float paper_shading = mix(0.82, 1.08, luminance);
                    vec3 gray_paper = background_color.rgb * paper_shading;
                    vec3 monochrome = mix(
                        gray_paper,
                        frame_color.rgb,
                        frame_mask
                    );

                    // h/s/v are deliberately retained for compatibility;
                    // only v affects the monochrome brightness.
                    float compatibility_value = clamp(
                        v + (h + s) * 0.000001,
                        0.0,
                        2.0
                    );
                    monochrome *= compatibility_value;

                    COLOR = vec4(
                        monochrome * vertex_color.rgb,
                        source.a * vertex_color.a
                    );
                }
                """,
        };

        return new ShaderMaterial
        {
            Shader = shader,
        };
    }
}
