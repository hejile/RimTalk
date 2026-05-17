use std::fs::File;
use std::sync::atomic::Ordering;
use std::thread;
use std::{ffi::CStr, sync::atomic::AtomicBool};
use std::os::raw::c_char;
use std::panic::catch_unwind;

use log::{info, error};

mod agent;
mod dto;
mod prompt;
mod request;
mod runtime;
mod rw_logger;

static FIRST_TICK: AtomicBool = AtomicBool::new(false);
static SETTINGS: std::sync::RwLock<dto::Settings> = std::sync::RwLock::new(dto::Settings {
    api_key: String::new(),
    provider: String::new(),
    model: String::new(),
});

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

#[unsafe(no_mangle)]
pub extern "C" fn update_settings(json_ptr: *const c_char) {
    catch_unwind(move || {
        if json_ptr.is_null() {
            return;
        }
        let c_str = unsafe { CStr::from_ptr(json_ptr) };
        if let Ok(json_data) = c_str.to_str() {
            match serde_json::from_str::<dto::Settings>(json_data) {
                Ok(settings) => {
                    info!("[RimAgent Rust] Received settings update: Provider={}, Model={}, API Key length={}", 
                        settings.provider,
                        settings.model,
                        settings.api_key.len());
                    if let Ok(mut global_settings) = SETTINGS.write() {
                        *global_settings = settings;
                    }
                }
                Err(e) => {
                    error!("[RimAgent Rust] Failed to parse settings: {}", e);
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
            runtime::start();
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

#[unsafe(no_mangle)]
pub extern "C" fn rust_exit() {
    runtime::exit();
}
