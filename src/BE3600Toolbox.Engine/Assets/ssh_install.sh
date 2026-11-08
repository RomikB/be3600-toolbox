#!/bin/sh

DIR_PATCH=/etc/crontabs/patches

# 1. Ensure patch directory exists
if [ ! -d "$DIR_PATCH" ]; then
	mkdir -p "$DIR_PATCH"
	chown root "$DIR_PATCH"
	chmod 0755 "$DIR_PATCH"
fi

# 2. Copy patch script
mv -f /tmp/ssh_patch.sh "$DIR_PATCH/"
chmod +x "$DIR_PATCH/ssh_patch.sh"

# 3. Execute patch
rm -f /tmp/ssh_patch.log
"$DIR_PATCH/ssh_patch.sh"

# 4. Enable autostart via cron
touch /etc/crontabs/root
grep -v "/ssh_patch.sh" /etc/crontabs/root > /etc/crontabs/root.new 2>/dev/null
echo "*/1 * * * * $DIR_PATCH/ssh_patch.sh >/dev/null 2>&1" >> /etc/crontabs/root.new
mv -f /etc/crontabs/root.new /etc/crontabs/root
/etc/init.d/cron enable
/etc/init.d/cron restart

# 5. Enable autostart via firewall
uci set firewall.auto_ssh_patch=include
uci set firewall.auto_ssh_patch.type='script'
uci set firewall.auto_ssh_patch.path="$DIR_PATCH/ssh_patch.sh"
uci set firewall.auto_ssh_patch.enabled='1'
uci commit firewall
