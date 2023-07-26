import * as THREE from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader";
import { log } from "../utils/utils";

export const createHeadset = () => {
  const group = new THREE.Group();
  const loader = new GLTFLoader();
  loader.load(
    "/quest2_hmd.glb",
    (gltf) => {
      // TODO: Bake these into the model
      gltf.scene.rotateX(Math.PI * -0.3);
      gltf.scene.rotateY(Math.PI);
      const mesh = gltf.scene.children[0] as THREE.Mesh;
      const material = mesh.material as THREE.MeshStandardMaterial;
      material.emissive = new THREE.Color(0.1, 0.1, 0.1);
      group.add(gltf.scene);
    },
    undefined,
    (err) => {
      log.error("Failed to load HMD model:", err);
    }
  );
  return group;
};
