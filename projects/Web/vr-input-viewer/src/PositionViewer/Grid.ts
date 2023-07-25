import * as THREE from "three";

export class Grid {
  plane: THREE.Mesh;
  material: THREE.ShaderMaterial;

  constructor(
    public radius: number,
    public spacing: number,
    public thickness: number
  ) {
    this.material = new THREE.ShaderMaterial({
      uniforms: this.calculateUniforms(),
      vertexShader: `
        varying vec2 vUv;
        uniform vec2 size;
        void main() {
          vUv = uv * size;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform float lineThickness;
        uniform float lineSpacing;
        uniform float fadeSize;
        uniform vec3 color;
        uniform vec2 size;
      
        varying vec2 vUv;
      
        float line(float a, float b, float line_width, float feather){
          float half_width = line_width/2.0;
          return smoothstep(a - half_width - feather, a - half_width, b) - 
                smoothstep(a + half_width - feather, a + half_width, b);
        }
      
        void main() {
          float lines = line(lineThickness, fract(vUv.x * lineSpacing / size.x), lineThickness, lineThickness/2.0);
          lines += line(lineThickness, fract(vUv.y * lineSpacing / size.y), lineThickness, lineThickness/2.0);
      
          vec2 worldUv = vUv / size;
          float dist_to_center = distance(worldUv, vec2(0.5));
          float fade = clamp(dist_to_center / (fadeSize / max(size.x, size.y)), 0.0, 1.0);
      
          vec4 finalColor = vec4(color * lines, 1.0 - fade);
      
          gl_FragColor = finalColor;
        }
      `,
      transparent: true,
    });
    const planeGeometry = new THREE.PlaneGeometry(1, 1);
    this.plane = new THREE.Mesh(planeGeometry, this.material);
    this.plane.rotateX(Math.PI / -2);
  }

  update() {
    this.plane.scale.set(this.radius * 2, this.radius * 2, this.radius * 2);
    this.material.uniforms = this.calculateUniforms();
  }

  private calculateUniforms() {
    return {
      size: {
        value: new THREE.Vector2(this.radius * 2, this.radius * 2),
      },
      lineThickness: { value: this.thickness },
      lineSpacing: { value: (this.radius * 2) / this.spacing },
      fadeSize: { value: this.radius },
      color: { value: new THREE.Color(0xffffff) },
    };
  }
}
