shader_type canvas_item;

/*
 * 青灰色普通卡框
 *
 * fragment() 中的 COLOR 已包含卡框纹理及 CanvasItem 调制色。
 * 这里保留原卡框的透明度和明暗结构，只把颜色映射为青灰色阶。
 */

uniform vec4 shadow_color : source_color = vec4(0.72, 0.73, 0.74, 1.0);
uniform vec4 middle_color : source_color = vec4(0.84, 0.85, 0.86, 1.0);
uniform vec4 highlight_color : source_color = vec4(0.95, 0.95, 0.96, 1.0);

uniform float contrast : hint_range(0.50, 1.80, 0.01) = 1.08;
uniform float detail_strength : hint_range(0.00, 0.40, 0.01) = 0.12;
uniform float brightness : hint_range(0.60, 1.40, 0.01) = 1.00;

void fragment()
{
    vec4 source = COLOR;

    float luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
    luminance = clamp((luminance - 0.5) * contrast + 0.5, 0.0, 1.0);

    vec3 mapped_color;
    if (luminance < 0.5)
    {
        mapped_color = mix(shadow_color.rgb, middle_color.rgb, luminance * 2.0);
    }
    else
    {
        mapped_color = mix(
            middle_color.rgb,
            highlight_color.rgb,
            (luminance - 0.5) * 2.0
        );
    }

    // 轻微保留原纹理的局部色差，使刻纹和磨损细节不显得过于平坦。
    float channel_spread = max(source.r, max(source.g, source.b))
        - min(source.r, min(source.g, source.b));
    vec3 original_detail = source.rgb - vec3(
        dot(source.rgb, vec3(0.333333))
    );

    mapped_color += original_detail
        * detail_strength
        * (0.5 + channel_spread);

    COLOR = vec4(
        clamp(mapped_color * brightness, vec3(0.0), vec3(1.0)),
        source.a
    );
}
