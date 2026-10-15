const base=['discover_instances','get_project_state','get_active_document','ping_revit','get_project_info','get_levels','query_elements','validate_elements','get_last_errors','capture_snapshot','read_snapshot','diff_snapshots'];
export const profiles={
 inspection:base,
 workflow:[...base,'save_project','run_workflow','apply_plan','set_window_sills','assign_marks','tag_rooms'],
 modeling:[...base,'save_project','create_elements','modify_elements','delete_elements','apply_plan','set_window_sills','assign_marks','tag_rooms'],
 diagnostic:[...base,'create_test_project'],
 full:null
};
export function selectTools(all,profile){if(!(profile in profiles))throw Error('Unknown MCP profile: '+profile);return profiles[profile]===null?all:all.filter(t=>profiles[profile].includes(t.name));}
