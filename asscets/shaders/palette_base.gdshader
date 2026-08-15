shader_type canvas_item;

// 双图层方案的Base层Shader：只查R通道调色板，无颜色检测
// BaseSprite（下层灰白图）使用此Shader
uniform sampler2D palette : repeat_disable, filter_nearest;

// 全局色调乘数（用于待机变暗、选中高亮等）
uniform vec4 tone : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    
    // 透明像素直接输出
    if (tex.a < 0.01) {
        COLOR = tex;
        return;
    }
    
    // R通道作为调色板索引（0=暗部，0.5=主色，1=高光）
    float index = clamp(tex.r, 0.0, 1.0);
    vec4 replaced = texture(palette, vec2(index, 0.0));
    
    // 应用色调乘数（待机变暗等）
    COLOR = vec4(replaced.rgb * tone.rgb, tex.a);
}
