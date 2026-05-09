use std::sync::atomic::Ordering;
use std::thread;
use std::{ffi::CStr, sync::atomic::AtomicBool};
use std::os::raw::c_char;
use std::panic::catch_unwind;

use log::{info, error};

mod dto;
mod rw_logger;

static LOGGER: rw_logger::RwLogger = rw_logger::RwLogger;
static FIRST_TICK: AtomicBool = AtomicBool::new(false);

#[unsafe(no_mangle)]
pub extern "C" fn get_rust_magic_number() -> i32 {
    42
}

#[unsafe(no_mangle)]
pub extern "C" fn update_game_info(json_ptr: *const c_char) {
    catch_unwind(move || {
        if json_ptr.is_null() {
            return;
        }
        let c_str = unsafe { CStr::from_ptr(json_ptr) };
        if let Ok(json_data) = c_str.to_str() {
            match serde_json::from_str::<dto::Game>(json_data) {
                Ok(game) => {
                    info!("[RimAgent Rust] Received game info update: {} members, {} maps", 
                        game.player_faction_members.len(), 
                        game.maps.len());
                }
                Err(e) => {
                    error!("[RimAgent Rust] Failed to parse game info: {}", e);
                }
            }
        }
    }).ok();
}

#[derive(serde::Serialize)]
#[serde(rename_all = "PascalCase")]
struct RustTickResponse {
    logs: Vec<rw_logger::LogEntry>,
}

#[unsafe(no_mangle)]
pub extern "C" fn rust_tick(last_response: *const c_char) -> *const c_char {
    catch_unwind(move || {
        if !last_response.is_null() {
            drop(unsafe { std::ffi::CString::from_raw(last_response as *mut c_char) });
        }
        if !FIRST_TICK.swap(true, Ordering::AcqRel) {
            start();
        }
        let logs = rw_logger::drain_logs();
        let response = RustTickResponse {
            logs,
        };
        let json_response = serde_json::to_string(&response).unwrap_or_else(|_| "{\"logs\":[]}".to_string());
        let c_string = std::ffi::CString::new(json_response).unwrap();
        c_string.into_raw()
    }).unwrap_or_else(|_| std::ptr::null_mut())
}

fn start() {
    log::set_logger(&LOGGER).unwrap();
    log::set_max_level(log::LevelFilter::Info);
    info!("[RimAgent Rust] Logger initialized.");
    thread::spawn(|| {
        info!("[RimAgent Rust] thread started.");
    });
}