#!/usr/bin/env bash
# Claude Code status line
# Reads JSON from stdin and prints a colour-coded status line.

input=$(cat)

# ── colours (ANSI, dimmed-terminal friendly) ────────────────────────────────
RESET='\033[0m'
BOLD='\033[1m'
DIM='\033[2m'

C_CYAN='\033[36m'
C_GREEN='\033[32m'
C_YELLOW='\033[33m'
C_RED='\033[31m'
C_BLUE='\033[34m'
C_MAGENTA='\033[35m'
C_WHITE='\033[37m'

# ── helpers ──────────────────────────────────────────────────────────────────
jq_val() { echo "$input" | jq -r "$1 // empty"; }

# ── data extraction ──────────────────────────────────────────────────────────
model=$(jq_val '.model.display_name')
cwd=$(jq_val '.workspace.current_dir')
used_pct=$(jq_val '.context_window.used_percentage')
remaining_pct=$(jq_val '.context_window.remaining_percentage')
input_tokens=$(jq_val '.context_window.current_usage.input_tokens')
output_tokens=$(jq_val '.context_window.current_usage.output_tokens')
cache_read=$(jq_val '.context_window.current_usage.cache_read_input_tokens')
five_h=$(jq_val '.rate_limits.five_hour.used_percentage')
seven_d=$(jq_val '.rate_limits.seven_day.used_percentage')
vim_mode=$(jq_val '.vim.mode')
session_name=$(jq_val '.session_name')
git_worktree=$(jq_val '.workspace.git_worktree')

# ── git branch (optional, skip locks) ────────────────────────────────────────
git_branch=""
if [ -n "$cwd" ] && command -v git >/dev/null 2>&1; then
    git_branch=$(git -C "$cwd" --no-optional-locks symbolic-ref --short HEAD 2>/dev/null)
fi

# ── context bar (10 chars wide) ───────────────────────────────────────────────
context_bar=""
if [ -n "$used_pct" ]; then
    pct_int=$(printf '%.0f' "$used_pct")
    filled=$(( pct_int * 10 / 100 ))
    empty=$(( 10 - filled ))
    bar=""
    for i in $(seq 1 $filled); do bar="${bar}█"; done
    for i in $(seq 1 $empty);  do bar="${bar}░"; done

    if   [ "$pct_int" -ge 85 ]; then bar_color="$C_RED"
    elif [ "$pct_int" -ge 60 ]; then bar_color="$C_YELLOW"
    else                              bar_color="$C_GREEN"
    fi
    context_bar=$(printf "${bar_color}${bar}${RESET} ${DIM}%d%%${RESET}" "$pct_int")
fi

# ── token summary ─────────────────────────────────────────────────────────────
token_info=""
if [ -n "$input_tokens" ] && [ "$input_tokens" != "0" ]; then
    fmt_k() { awk "BEGIN{printf \"%.1fk\", $1/1000}"; }
    in_k=$(fmt_k "$input_tokens")
    out_k=$(fmt_k "$output_tokens")
    token_info=$(printf "${DIM}in:${RESET}${C_WHITE}%s${RESET} ${DIM}out:${RESET}${C_WHITE}%s${RESET}" "$in_k" "$out_k")
    if [ -n "$cache_read" ] && [ "$cache_read" != "0" ]; then
        cr_k=$(fmt_k "$cache_read")
        token_info="${token_info} $(printf "${DIM}cache:${RESET}${C_CYAN}%s${RESET}" "$cr_k")"
    fi
fi

# ── rate limits ───────────────────────────────────────────────────────────────
rate_info=""
if [ -n "$five_h" ]; then
    five_int=$(printf '%.0f' "$five_h")
    if   [ "$five_int" -ge 85 ]; then rl_color="$C_RED"
    elif [ "$five_int" -ge 60 ]; then rl_color="$C_YELLOW"
    else                               rl_color="$C_GREEN"
    fi
    rate_info=$(printf "${DIM}5h:${RESET}${rl_color}%d%%${RESET}" "$five_int")
fi
if [ -n "$seven_d" ]; then
    seven_int=$(printf '%.0f' "$seven_d")
    if   [ "$seven_int" -ge 85 ]; then rl2_color="$C_RED"
    elif [ "$seven_int" -ge 60 ]; then rl2_color="$C_YELLOW"
    else                                rl2_color="$C_GREEN"
    fi
    rate_info="${rate_info}$([ -n "$rate_info" ] && echo " ")$(printf "${DIM}7d:${RESET}${rl2_color}%d%%${RESET}" "$seven_int")"
fi

# ── assemble parts ────────────────────────────────────────────────────────────
parts=()

# model
if [ -n "$model" ]; then
    parts+=("$(printf "${C_MAGENTA}${BOLD}%s${RESET}" "$model")")
fi

# context bar
if [ -n "$context_bar" ]; then
    parts+=("$(printf "${DIM}ctx${RESET} %s" "$context_bar")")
fi

# rate limits
if [ -n "$rate_info" ]; then
    parts+=("$rate_info")
fi

# working directory (shortened: replace $HOME with ~)
if [ -n "$cwd" ]; then
    short_cwd="${cwd/#$HOME/~}"
    parts+=("$(printf "${C_CYAN}%s${RESET}" "$short_cwd")")
fi

# git branch / worktree
git_label=""
if [ -n "$git_worktree" ]; then
    git_label="$git_worktree"
elif [ -n "$git_branch" ]; then
    git_label="$git_branch"
fi
if [ -n "$git_label" ]; then
    parts+=("$(printf "${C_YELLOW} %s${RESET}" "$git_label")")
fi

# session name
if [ -n "$session_name" ]; then
    parts+=("$(printf "${C_BLUE}\"%s\"${RESET}" "$session_name")")
fi

# token info
if [ -n "$token_info" ]; then
    parts+=("$token_info")
fi

# vim mode
if [ -n "$vim_mode" ]; then
    if [ "$vim_mode" = "NORMAL" ]; then vm_color="$C_YELLOW"; else vm_color="$C_GREEN"; fi
    parts+=("$(printf "${vm_color}${BOLD}%s${RESET}" "$vim_mode")")
fi

# ── join with separator and print ─────────────────────────────────────────────
sep=$(printf " ${DIM}│${RESET} ")
result=""
for part in "${parts[@]}"; do
    if [ -z "$result" ]; then
        result="$part"
    else
        result="${result}${sep}${part}"
    fi
done

printf "%b\n" "$result"
