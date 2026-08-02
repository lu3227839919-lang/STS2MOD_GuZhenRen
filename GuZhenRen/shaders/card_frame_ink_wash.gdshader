shader_type canvas_item;
render_mode unshaded;

/*
 * 蛊真人默认水墨卡框。
 *
 * 该材质叠加在游戏原生卡框纹理上：原纹理继续决定攻击、技能、能力牌
 * 的轮廓和透明区域，着色器只重绘色调与笔触。因此不会遮挡卡图、标题、
 * 描述、稀有度装饰或能量图标。
 */

uniform vec4 ink_color : source_color = vec4(0.035, 0.045, 0.043, 1.0);
uniform vec4 deep_wash_color : source_color = vec4(0.12, 0.095, 0.035, 1.0);
uniform vec4 jade_wash_color : source_color = vec4(0.48, 0.34, 0.08, 1.0);
uniform vec4 paper_color : source_color = vec4(0.78, 0.69, 0.42, 1.0);
uniform vec4 cinnabar_color : source_color = vec4(0.62, 0.30, 0.035, 1.0);

uniform float contrast : hint_range(0.60, 1.80, 0.01) = 1.16;
uniform float paper_grain : hint_range(0.00, 0.30, 0.01) = 0.09;
uniform float wash_strength : hint_range(0.00, 0.60, 0.01) = 0.26;
uniform float outline_strength : hint_range(0.00, 1.00, 0.01) = 0.72;
uniform float dry_brush_strength : hint_range(0.00, 0.50, 0.01) = 0.16;
uniform float seal_strength : hint_range(0.00, 1.00, 0.01) = 0.82;

float hash21(vec2 value)
{
    value = fract(value * vec2(123.34, 456.21));
    value += dot(value, value + 45.32);
    return fract(value.x * value.y);
}

float value_noise(vec2 value)
{
    vec2 cell = floor(value);
    vec2 local = fract(value);
    local = local * local * (3.0 - 2.0 * local);

    float bottom = mix(
        hash21(cell),
        hash21(cell + vec2(1.0, 0.0)),
        local.x
    );
    float top = mix(
        hash21(cell + vec2(0.0, 1.0)),
        hash21(cell + vec2(1.0, 1.0)),
        local.x
    );
    return mix(bottom, top, local.y);
}

float ink_noise(vec2 uv)
{
    float result = value_noise(uv * 5.0) * 0.52;
    result += value_noise(uv * 13.0 + vec2(3.7, 8.1)) * 0.29;
    result += value_noise(uv * 41.0 + vec2(17.0, 5.0)) * 0.13;
    result += value_noise(uv * 113.0) * 0.06;
    return result;
}

float rectangle_mask(vec2 uv, vec2 center, vec2 half_size, float softness)
{
    vec2 delta = abs(uv - center) - half_size;
    float distance_to_edge = length(max(delta, vec2(0.0)))
        + min(max(delta.x, delta.y), 0.0);
    return 1.0 - smoothstep(-softness, softness, distance_to_edge);
}

float seal_mark(vec2 uv, float grain)
{
    const vec2 center = vec2(0.805, 0.855);
    const vec2 half_size = vec2(0.034, 0.043);

    float body = rectangle_mask(uv, center, half_size, 0.0025);
    float inner = rectangle_mask(
        uv,
        center,
        half_size - vec2(0.0065),
        0.0018
    );
    float border = clamp(body - inner, 0.0, 1.0);

    // 三道不完全对称的篆刻留白，避免依赖字体资源。
    float vertical = rectangle_mask(
        uv,
        center + vec2(-0.008, 0.001),
        vec2(0.0022, 0.023),
        0.0015
    );
    float upper = rectangle_mask(
        uv,
        center + vec2(0.006, -0.013),
        vec2(0.013, 0.0022),
        0.0015
    );
    float middle = rectangle_mask(
        uv,
        center + vec2(0.007, 0.001),
        vec2(0.010, 0.0020),
        0.0015
    );
    float lower = rectangle_mask(
        uv,
        center + vec2(0.004, 0.015),
        vec2(0.013, 0.0022),
        0.0015
    );

    float carved_lines = clamp(
        vertical + upper + middle + lower,
        0.0,
        1.0
    );
    float worn_body = body * smoothstep(0.32, 0.58, grain);
    return max(border, worn_body * (1.0 - carved_lines));
}

float sample_luminance(vec3 color)
{
    return dot(color, vec3(0.299, 0.587, 0.114));
}

void fragment()
{
    vec4 source = COLOR;
    vec2 pixel = TEXTURE_PIXEL_SIZE;

    float luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
    luminance = clamp(
        (luminance - 0.5) * contrast + 0.5,
        0.0,
        1.0
    );

    float broad_grain = ink_noise(UV);
    float fiber_grain = value_noise(
        vec2(UV.x * 310.0 + UV.y * 19.0, UV.y * 92.0)
    );
    float grain = mix(broad_grain, fiber_grain, 0.30);

    vec3 mapped_color;
    if (luminance < 0.46)
    {
        mapped_color = mix(
            ink_color.rgb,
            deep_wash_color.rgb,
            luminance / 0.46
        );
    }
    else
    {
        mapped_color = mix(
            deep_wash_color.rgb,
            paper_color.rgb,
            (luminance - 0.46) / 0.54
        );
    }

    // 中间调形成青灰墨晕；亮部只保留很轻的宣纸纤维。
    float middle_tone = 1.0 - abs(luminance - 0.48) * 2.0;
    float wash_field = smoothstep(0.28, 0.78, broad_grain)
        * clamp(middle_tone, 0.0, 1.0);
    mapped_color = mix(
        mapped_color,
        jade_wash_color.rgb,
        wash_field * wash_strength
    );
    mapped_color += (fiber_grain - 0.5)
        * paper_grain
        * smoothstep(0.40, 1.0, luminance);

    // 从原纹理的明暗和透明边界提取一圈积墨，增强手绘轮廓。
    float center_alpha = texture(TEXTURE, UV).a;
    float alpha_edge = 0.0;
    alpha_edge += abs(
        center_alpha - texture(
            TEXTURE,
            UV + vec2(pixel.x, 0.0)
        ).a
    );
    alpha_edge += abs(
        center_alpha - texture(
            TEXTURE,
            UV - vec2(pixel.x, 0.0)
        ).a
    );
    alpha_edge += abs(
        center_alpha - texture(
            TEXTURE,
            UV + vec2(0.0, pixel.y)
        ).a
    );
    alpha_edge += abs(
        center_alpha - texture(
            TEXTURE,
            UV - vec2(0.0, pixel.y)
        ).a
    );

    float center_luminance = sample_luminance(
        texture(TEXTURE, UV).rgb
    );
    float luma_edge = 0.0;
    luma_edge += abs(
        center_luminance - sample_luminance(
            texture(
                TEXTURE,
                UV + vec2(pixel.x, 0.0)
            ).rgb
        )
    );
    luma_edge += abs(
        center_luminance - sample_luminance(
            texture(
                TEXTURE,
                UV - vec2(pixel.x, 0.0)
            ).rgb
        )
    );
    luma_edge += abs(
        center_luminance - sample_luminance(
            texture(
                TEXTURE,
                UV + vec2(0.0, pixel.y)
            ).rgb
        )
    );
    luma_edge += abs(
        center_luminance - sample_luminance(
            texture(
                TEXTURE,
                UV - vec2(0.0, pixel.y)
            ).rgb
        )
    );

    float ink_edge = smoothstep(
        0.055,
        0.54,
        alpha_edge * 0.85 + luma_edge * 0.52
    );
    mapped_color = mix(
        mapped_color,
        ink_color.rgb,
        ink_edge * outline_strength
    );

    // 深色区域随机露出细小干笔断纹，保持静态，不产生动画闪烁。
    float dry_brush = smoothstep(0.68, 0.92, fiber_grain)
        * (1.0 - smoothstep(0.38, 0.72, luminance));
    mapped_color = mix(
        mapped_color,
        paper_color.rgb * 0.62,
        dry_brush * dry_brush_strength
    );

    // 朱砂印仅落在原卡框已有的不透明区域，不侵入卡图透明窗口。
    float seal = seal_mark(UV, grain)
        * source.a
        * seal_strength;
    mapped_color = mix(
        mapped_color,
        cinnabar_color.rgb,
        seal
    );

    float broken_edge = ink_edge
        * smoothstep(0.72, 0.92, broad_grain)
        * dry_brush_strength
        * 0.35;
    float final_alpha = source.a * (1.0 - broken_edge);

    COLOR = vec4(
        clamp(mapped_color, vec3(0.0), vec3(1.0)),
        final_alpha
    );
}
