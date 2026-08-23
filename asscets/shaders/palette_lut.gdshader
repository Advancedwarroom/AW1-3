shader_type canvas_item;

// ==================== 模式选择 ====================
// 0 = LUT模式（贴图R通道作为调色板索引）
// 1 = 兼容模式（自动检测红/蓝区域并替换，适配现有红蓝双色贴图）
uniform int mode : hint_range(0, 1) = 1;

// ==================== LUT模式参数 ====================
uniform sampler2D palette : repeat_disable, filter_nearest;

// ==================== 兼容模式参数 ====================
uniform vec4 primary_color : source_color = vec4(1.0, 0.2, 0.2, 1.0);   // 势力主色
uniform vec4 secondary_color : source_color = vec4(0.2, 0.4, 1.0, 1.0);  // 势力副色

// 颜色检测阈值（在Inspector中可调，找到最适合你贴图的值）
uniform float red_threshold : hint_range(0.0, 0.8) = 0.2;      // R比G/B高多少算红区
uniform float blue_threshold : hint_range(0.0, 0.8) = 0.2;     // B比R/G高多少算蓝区
uniform float sat_threshold : hint_range(0.0, 1.0) = 0.35;     // 饱和度门槛（0.35=排除肤色/棕色）
uniform float min_channel : hint_range(0.0, 1.0) = 0.45;       // 主导色通道最低值（R>0.45或B>0.45）
uniform float max_other : hint_range(0.0, 0.8) = 0.4;          // 其他通道最高值（排除偏橙/肤色）

// ==================== 通用参数 ====================
// 全局色调乘数（用于待机变暗、选中高亮等）
uniform vec4 tone : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// 是否启用着色（false=原图直出）
uniform bool enabled = true;

// P-1/P0 特殊色调（可选覆盖）
uniform vec4 override_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform bool use_override = false;

// ==================== 辅助函数 ====================
// 计算饱和度 (0~1)
float get_saturation(vec3 rgb) {
    float max_c = max(max(rgb.r, rgb.g), rgb.b);
    float min_c = min(min(rgb.r, rgb.g), rgb.b);
    return max_c - min_c;
}

// 检测是否是红色模板区域
// 条件：R够高、G和B够低、饱和度够高（排除肤色/棕色/灰色）
bool is_red_dominant(vec3 rgb) {
    float sat = get_saturation(rgb);
    if (sat < sat_threshold) return false;       // 饱和度太低=肤色/灰/棕，跳过
    if (rgb.r < min_channel) return false;       // R不够高
    if (rgb.g > max_other || rgb.b > max_other) return false; // G或B太高=偏橙/偏紫，跳过
    return (rgb.r - rgb.g > red_threshold) && (rgb.r - rgb.b > red_threshold);
}

// 检测是否是蓝色模板区域
bool is_blue_dominant(vec3 rgb) {
    float sat = get_saturation(rgb);
    if (sat < sat_threshold) return false;
    if (rgb.b < min_channel) return false;
    if (rgb.r > max_other || rgb.g > max_other) return false;
    return (rgb.b - rgb.r > blue_threshold) && (rgb.b - rgb.g > blue_threshold);
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    
    // 完全透明或禁用时不处理
    if (!enabled || tex.a < 0.01) {
        COLOR = tex * tone;
    }
    else {
        vec3 rgb = tex.rgb;
        vec3 result = rgb;
        
        if (mode == 0) {
            // ===== LUT模式：R通道作为调色板索引 =====
            float index = clamp(tex.r, 0.0, 1.0);
            vec2 palette_uv = vec2(index, 0.0);
            vec4 replaced = texture(palette, palette_uv);
            result = replaced.rgb;
        }
        else {
            // ===== 兼容模式：红蓝区域自动检测替换 =====
            if (is_red_dominant(rgb)) {
                // 红色模板区 → 替换为势力主色（保留原明暗层次）
                float luminance = dot(rgb, vec3(0.299, 0.587, 0.114));
                result = primary_color.rgb * (0.4 + luminance * 1.2);
            }
            else if (is_blue_dominant(rgb)) {
                // 蓝色模板区 → 替换为势力副色
                float luminance = dot(rgb, vec3(0.299, 0.587, 0.114));
                result = secondary_color.rgb * (0.4 + luminance * 1.2);
            }
            // 其他颜色（肤色、金属、阴影等）保持不变
        }
        
        // P-1/P0 特殊色调覆盖
        if (use_override) {
            float luminance = dot(rgb, vec3(0.299, 0.587, 0.114));
            result = override_color.rgb * luminance * 1.5;
        }
        
        // 应用全局色调（待机变暗等）
        COLOR = vec4(result * tone.rgb, tex.a * tone.a);
    }
}
