import {finalizePresentation} from 'file:///C:/Users/pierr/.codex/plugins/cache/openai-primary-runtime/presentations/26.1026.10807/skills/presentations/container_tools/artifact_tool_utils.mjs';
process.env.RUNTIME_NODE_MODULES='C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const skill='C:/Users/pierr/.codex/plugins/cache/openai-primary-runtime/presentations/26.1026.10807/skills/presentations';
const r=await finalizePresentation({
 workspaceDir:'D:/project/ChessRPG',
 candidatePath:'D:/project/ChessRPG/tmp/ppt-story/candidate.pptx',
 finalPath:'D:/project/ChessRPG/output/presentations/ChessRPG_成果發表簡報_卡牌故事版.pptx',
 pythonExecutable:'C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 integrityValidatorPath:skill+'/container_tools/inspect_presentation_package_integrity.py',
 layoutValidatorPath:skill+'/container_tools/inspect_presentation_layout_geometry.py',
 layoutArgs:['--expected-slide-size-emu','9144000,5143500','--validate-bullet-geometry','--validate-heading-fit','--require-native-table-slide','14','--require-native-table-slide','15'],
 requiredNativeTableOwnerSlides:[14,15],
 verifyArtifactToolImport:true,
 receiptPath:'D:/project/ChessRPG/tmp/ppt-story/validation-v2.json'
});
console.log(JSON.stringify(r));



