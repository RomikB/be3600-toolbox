#!/bin/sh

DIR_PATCH=/etc/crontabs/patches

# 1. Check required translation files
if [ `find /tmp -maxdepth 1 -name 'base.*.lmo' 2>/dev/null | wc -l` -eq 0 ] && \
   [ `find "$DIR_PATCH" -maxdepth 1 -name 'base.*.lmo' 2>/dev/null | wc -l` -eq 0 ]; then
	exit 1
fi

# 2. Ensure patch directory exists
if [ ! -d "$DIR_PATCH" ]; then
	mkdir -p "$DIR_PATCH"
	chown root "$DIR_PATCH"
	chmod 0755 "$DIR_PATCH"
fi

# 3. Copy patch and auxiliary translation files
mv -f /tmp/lang_patch.sh "$DIR_PATCH/"
chmod +x "$DIR_PATCH/lang_patch.sh"

if [ `find /tmp -maxdepth 1 -name 'base.*.lmo' 2>/dev/null | wc -l` -gt 0 ]; then
	mv -f /tmp/base.*.lmo "$DIR_PATCH/"
fi

# 4. Set main language and execute patch
uci set luci.main.lang=en
rm -f /tmp/lang_patch.log
"$DIR_PATCH/lang_patch.sh"

# 5. Enable autostart via cron
touch /etc/crontabs/root
grep -v "/lang_patch.sh" /etc/crontabs/root > /etc/crontabs/root.new 2>/dev/null
echo "*/1 * * * * $DIR_PATCH/lang_patch.sh >/dev/null 2>&1" >> /etc/crontabs/root.new
mv -f /etc/crontabs/root.new /etc/crontabs/root
/etc/init.d/cron enable
/etc/init.d/cron restart

# 6. Enable autostart via firewall
uci set firewall.auto_lang_patch=include
uci set firewall.auto_lang_patch.type='script'
uci set firewall.auto_lang_patch.path="$DIR_PATCH/lang_patch.sh"
uci set firewall.auto_lang_patch.enabled='1'
uci commit firewall
