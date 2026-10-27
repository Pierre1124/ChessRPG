import {finalizePresentation} from 'file:///C:/Users/pierr/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations/container_tools/artifact_tool_utils.mjs';
process.env.RUNTIME_NODE_MODULES='C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const skill='C:/Users/pierr/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations';
const r=await finalizePresentation({
 workspaceDir:'D:/project/ChessRPG',
 candidatePath:'D:/project/ChessRPG/tmp/ppt-revision/candidate.pptx',
 finalPath:'D:/project/ChessRPG/output/presentations/ChessRPG_成果發表簡報_修改版.pptx',
 pythonExecutable:'C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 integrityValidatorPath:skill+'/container_tools/inspect_presentation_package_integrity.py',
 layoutValidatorPath:skill+'/container_tools/inspect_presentation_layout_geometry.py',
 layoutArgs:['--expected-slide-size-emu','9144000,5143500','--validate-bullet-geometry','--validate-heading-fit','--require-native-table-slide','9','--require-native-table-slide','10'],
 requiredNativeTableOwnerSlides:[9,10],
 verifyArtifactToolImport:true,
 receiptPath:'D:/project/ChessRPG/tmp/ppt-revision/validation.json'
});
console.log(JSON.stringify(r));
