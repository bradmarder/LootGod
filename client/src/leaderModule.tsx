import { useState, useEffect } from 'react';
import { Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';

export default function leaderModule() {

	const [isLoading, setIsLoading] = useState(true);
	const [raidDiscord, setRaidDiscord] = useState('');
	const [rotDiscord, setRotDiscord] = useState('');
	const [transferName, setTransferName] = useState('');
	const [discordSuccess, setDiscordSuccess] = useState(false);
	const [transferSuccess, setTransferSuccess] = useState(false);

	const transferLeadership = () => {
		setIsLoading(true);
		axios
			.post('/TransferGuildLeadership?name=' + transferName)
			.then(_ => setTransferSuccess(true))
			.finally(() => setIsLoading(false));
	};
	const updateDiscord = async () => {
		setIsLoading(true);
		setDiscordSuccess(false);
		const a = axios.post('/GuildDiscord?raidNight=true&webhook=' + encodeURIComponent(raidDiscord));
		const b = axios.post('/GuildDiscord?raidNight=false&webhook=' + encodeURIComponent(rotDiscord));
		try {
			await Promise.all([a, b]);
			setDiscordSuccess(true)
		} finally {
			setIsLoading(false);
		}
	};
	const getDiscord = () => {
		axios
			.get<{ raid: string, rot: string }>('/GetDiscordWebhooks')
			.then(x => {
				setRaidDiscord(x.data.raid);
				setRotDiscord(x.data.rot);
			})
			.finally(() => setIsLoading(false));
	};
	useEffect(getDiscord, []);

	return (
		<Alert variant='dark'>
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Transfer Guild Leadership</Form.Label>
					<Form.Control type="text" placeholder='Enter new guild leader name' value={transferName} onChange={e => setTransferName(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={isLoading || transferName.length < 4} onClick={transferLeadership}>Transfer</Button>
				{transferSuccess &&
					<Alert variant='success'>Successfully transferred leadership</Alert>
				}
			</Form>
			<hr />
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Raid Discord Webhook URL</Form.Label>
					<Form.Control type="text" placeholder='Enter Raid Discord webhook URL' value={raidDiscord} onChange={e => setRaidDiscord(e.target.value)} />
				</Form.Group>
				<hr />
				<Form.Group>
					<Form.Label>Rot Discord Webhook URL</Form.Label>
					<Form.Control type="text" placeholder='Enter Rot Discord webhook URL' value={rotDiscord} onChange={e => setRotDiscord(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={isLoading} onClick={updateDiscord}>Update</Button>
				{discordSuccess &&
					<Alert variant='success'>Successfully updated Discord webhook URL</Alert>
				}
			</Form>
		</Alert>
	);
}